package kr.aion.heartstream

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Intent
import android.content.IntentFilter
import android.content.pm.PackageManager
import android.hardware.Sensor
import android.hardware.SensorEvent
import android.hardware.SensorEventListener
import android.hardware.SensorManager
import android.net.wifi.WifiManager
import android.os.BatteryManager
import android.os.Build
import android.os.Handler
import android.os.HandlerThread
import android.os.IBinder
import android.os.PowerManager
import android.os.SystemClock
import android.util.Log
import androidx.health.services.client.HealthServices
import androidx.health.services.client.MeasureCallback
import androidx.health.services.client.data.Availability
import androidx.health.services.client.data.DataPointContainer
import androidx.health.services.client.data.DataType
import androidx.health.services.client.data.DataTypeAvailability
import androidx.health.services.client.data.DeltaDataType
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicInteger
import kotlin.math.sqrt

class HeartService : Service() {
    companion object {
        @Volatile var running = false
        @Volatile var bpm: Double? = null
        @Volatile var count = 0L
        @Volatile var acceleration: Double? = null
        @Volatile var angularSpeed: Double? = null
    }
    private val executor = Executors.newSingleThreadScheduledExecutor()
    private val measure by lazy { HealthServices.getClient(this).measureClient }
    private var socket: DatagramSocket? = null
    private var lastSend = 0L
    private var seq = 0L
    private var registered = false
    private val imu = ImuBuffer()
    private var imuSeq = 0L
    private val imuSamples = AtomicInteger()
    private val hrCallbacks = AtomicInteger()
    private var hrSent = 0
    private var lastMeasuredBpm: Double? = null
    private var imuSent = 0
    private var failures = 0
    private val limiter = ErrorLimiter()
    private var lastAvailability = ""
    private lateinit var sensorManager: SensorManager
    private var sensorThread: HandlerThread? = null
    private var wakeLock: PowerManager.WakeLock? = null
    private val listener = object : SensorEventListener {
        override fun onSensorChanged(event: SensorEvent) {
            imu.add(event.sensor.type == Sensor.TYPE_ACCELEROMETER, event.values[0].toDouble(), event.values[1].toDouble(), event.values[2].toDouble())
            imuSamples.incrementAndGet()
        }
        override fun onAccuracyChanged(sensor: Sensor?, accuracy: Int) { }
    }
    private fun diag(level: String, event: String, fields: String = "") = DiagLog.event(this, level, event, fields)
    private val callback = object : MeasureCallback {
        override fun onAvailabilityChanged(dataType: DeltaDataType<*, *>, availability: Availability) {
            val name = availability.toString()
            synchronized(this@HeartService) { if (lastAvailability != name) { lastAvailability = name; diag("INFO", "avail", "state=$name") } }
            if (availability != DataTypeAvailability.AVAILABLE) enqueue(null)
        }
        override fun onDataReceived(data: DataPointContainer) {
            hrCallbacks.incrementAndGet()
            data.getData(DataType.HEART_RATE_BPM).forEach { enqueue(it.value) }
        }
    }
    private fun enqueue(value: Double?) {
        if (!executor.isShutdown) try { executor.execute { send(value) } } catch (_: java.util.concurrent.RejectedExecutionException) { }
    }
    override fun onCreate() {
        super.onCreate()
        diag("INFO", "svc_create", "reason=service_created")
        val manager = getSystemService(NotificationManager::class.java)
        manager.createNotificationChannel(NotificationChannel("heart", "심박 송신", NotificationManager.IMPORTANCE_LOW))
        val pending = PendingIntent.getActivity(this, 0, Intent(this, MainActivity::class.java), PendingIntent.FLAG_IMMUTABLE)
        val notification = Notification.Builder(this, "heart").setContentTitle("심박 송신 중").setSmallIcon(android.R.drawable.ic_menu_info_details).setContentIntent(pending).setOngoing(true).build()
        if (Build.VERSION.SDK_INT >= 34) startForeground(1, notification, android.content.pm.ServiceInfo.FOREGROUND_SERVICE_TYPE_HEALTH) else startForeground(1, notification)
        val prefs = getSharedPreferences("destination", MODE_PRIVATE)
        val permission = if (Build.VERSION.SDK_INT >= 36) "android.permission.health.READ_HEART_RATE" else android.Manifest.permission.BODY_SENSORS
        diag("INFO", "svc_start", "host=${prefs.getString("host", "")} port=${prefs.getInt("port", 11126)} permission=${checkSelfPermission(permission) == PackageManager.PERMISSION_GRANTED} sdk=${Build.VERSION.SDK_INT} model=${Build.MODEL}")
        if (prefs.getString("host", "").isNullOrBlank()) { stopSelf(); return }
        running = true; bpm = null; count = 0
        try {
            wakeLock = getSystemService(PowerManager::class.java).newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, "HeartStream:imu").apply { acquire(60 * 60 * 1000L) }
            diag("INFO", "wakelock_acquire", "timeout_ms=3600000")
        } catch (e: Exception) { diag("WARN", "wakelock_acquire", "error=${e.javaClass.simpleName}") }
        sensorManager = getSystemService(SensorManager::class.java)
        sensorThread = HandlerThread("HeartStreamSensors").also { it.start() }
        for (type in listOf(Sensor.TYPE_ACCELEROMETER, Sensor.TYPE_GYROSCOPE)) {
            val sensor = sensorManager.getDefaultSensor(type)
            val ok = try { sensor != null && sensorManager.registerListener(listener, sensor, SensorManager.SENSOR_DELAY_GAME, Handler(sensorThread!!.looper)) } catch (e: Exception) { false }
            diag(if (ok) "INFO" else "WARN", "sensor_register", "type=$type success=$ok name=${sensor?.name ?: "none"}")
        }
        executor.scheduleWithFixedDelay({ if (SystemClock.elapsedRealtime() - lastSend >= 1000) send(null); sendImu() }, 0, 100, TimeUnit.MILLISECONDS)
        executor.scheduleWithFixedDelay({ diag("INFO", "stat", "hr_cb=${hrCallbacks.getAndSet(0)} hr_sent=$hrSent imu_samples=${imuSamples.getAndSet(0)} imu_sent=$imuSent send_fail=$failures bpm=${lastMeasuredBpm ?: "none"} seq=$seq imuSeq=$imuSeq"); hrSent = 0; imuSent = 0; failures = 0 }, 1, 1, TimeUnit.SECONDS)
        executor.scheduleWithFixedDelay({ logEnv() }, 0, 10, TimeUnit.SECONDS)
        try {
            val capabilities = measure.getCapabilitiesAsync()
            capabilities.addListener({
                try {
                    val supported = capabilities.get().supportedDataTypesMeasure
                    diag("INFO", "caps", "types=$supported")
                    if (DataType.HEART_RATE_BPM in supported) {
                        synchronized(this) {
                            if (running) { measure.registerMeasureCallback(DataType.HEART_RATE_BPM, callback); registered = true; diag("INFO", "hr_register_ok") }
                        }
                    } else diag("WARN", "hr_register_fail", "reason=unsupported")
                } catch (e: Exception) { diag("ERROR", "hr_register_fail", "reason=${e.javaClass.simpleName}") }
            }, executor)
        } catch (e: Exception) { diag("ERROR", "hr_register_fail", "reason=${e.javaClass.simpleName}") }
    }
    private fun send(value: Double?) {
        val valid = value?.takeIf { it.isFinite() && it > 0.0 }
        bpm = valid
        if (valid != null) lastMeasuredBpm = valid
        if (sendPacket(PacketBuilder.build(seq, System.currentTimeMillis(), valid))) { seq++; count = seq; hrSent++ }
        lastSend = SystemClock.elapsedRealtime()
    }
    private fun sendImu() {
        val sample = imu.drain() ?: return
        fun magnitude(v: List<Double>) = sqrt(v.sumOf { it * it })
        acceleration = magnitude(sample.a); angularSpeed = magnitude(sample.g)
        val packet = PacketBuilder.buildImu(imuSeq, System.currentTimeMillis(), sample.a[0], sample.a[1], sample.a[2], sample.g[0], sample.g[1], sample.g[2], sample.am, sample.gm, sample.n) ?: return
        if (sendPacket(packet)) { imuSeq++; imuSent++ }
    }
    private fun sendPacket(packet: String): Boolean {
        try {
            val prefs = getSharedPreferences("destination", MODE_PRIVATE)
            val host = prefs.getString("host", "").orEmpty()
            if (host.isBlank()) return false
            val port = prefs.getInt("port", 11126)
            val bytes = packet.toByteArray(Charsets.UTF_8)
            val target = InetAddress.getByName(host)
            synchronized(this) {
                if (!running) return false
                val udp = socket ?: DatagramSocket().also { socket = it }
                udp.send(DatagramPacket(bytes, bytes.size, target, port))
            }
            return true
        } catch (e: Exception) {
            failures++
            val message = (e.message ?: "").take(120)
            if (limiter.allow("${e.javaClass.name}:$message", SystemClock.elapsedRealtime())) diag("WARN", "send_fail", "class=${e.javaClass.simpleName} message=$message")
            Log.w("HeartStream", "UDP send failed", e)
            return false
        }
    }
    private fun logEnv() {
        val battery = registerReceiver(null, IntentFilter(Intent.ACTION_BATTERY_CHANGED))
        val level = battery?.getIntExtra(BatteryManager.EXTRA_LEVEL, -1) ?: -1
        val scale = battery?.getIntExtra(BatteryManager.EXTRA_SCALE, -1) ?: -1
        val percent = if (level >= 0 && scale > 0) (100 * level / scale).toString() else "n/a"
        val plugged = battery?.getIntExtra(BatteryManager.EXTRA_PLUGGED, 0)?.let { it != 0 }?.toString() ?: "n/a"
        val wifi = try {
            @Suppress("DEPRECATION")
            val info = applicationContext.getSystemService(WifiManager::class.java).connectionInfo
            @Suppress("DEPRECATION")
            "connected=${info.networkId != -1} rssi=${info.rssi}"
        } catch (_: Exception) { "connected=n/a rssi=n/a" }
        diag("INFO", "env", "battery=$percent charging=$plugged wifi_$wifi")
    }
    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int) = START_NOT_STICKY
    override fun onDestroy() {
        synchronized(this) {
            running = false
            if (registered) { try { measure.unregisterMeasureCallbackAsync(DataType.HEART_RATE_BPM, callback) } catch (e: Exception) { Log.w("HeartStream", "Unregister failed", e) } }
            socket?.close(); socket = null
        }
        if (::sensorManager.isInitialized) sensorManager.unregisterListener(listener)
        sensorThread?.quitSafely()
        wakeLock?.let { if (it.isHeld) it.release(); diag("INFO", "wakelock_release", "reason=service_destroy") }
        diag("INFO", "svc_destroy", "reason=service_stopped")
        executor.shutdownNow(); bpm = null
        acceleration = null; angularSpeed = null
        super.onDestroy()
    }
    override fun onBind(intent: Intent?): IBinder? = null
}
