package kr.aion.heartstream

import android.Manifest
import android.app.Activity
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Build
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.view.Gravity
import android.view.WindowManager
import android.widget.Button
import android.widget.LinearLayout
import android.widget.TextView

class MainActivity : Activity() {
    private val handler = Handler(Looper.getMainLooper())
    private lateinit var status: TextView
    private lateinit var button: Button
    private val permission get() = if (Build.VERSION.SDK_INT >= 36) "android.permission.health.READ_HEART_RATE" else Manifest.permission.BODY_SENSORS
    private val refresh = object : Runnable {
        override fun run() { update(); handler.postDelayed(this, 500) }
    }
    override fun onCreate(state: Bundle?) {
        super.onCreate(state)
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        saveExtras(intent)
        val layout = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL; gravity = Gravity.CENTER; setPadding(40, 32, 40, 32) }
        status = TextView(this).apply { gravity = Gravity.CENTER; textSize = 11f }
        button = Button(this).apply { setOnClickListener { toggle() } }
        layout.addView(status); layout.addView(button); setContentView(layout)
        if (checkSelfPermission(permission) != PackageManager.PERMISSION_GRANTED) requestPermissions(arrayOf(permission), 1)
    }
    override fun onNewIntent(intent: Intent) { super.onNewIntent(intent); setIntent(intent); saveExtras(intent); update() }
    private fun saveExtras(intent: Intent) {
        val prefs = getSharedPreferences("destination", MODE_PRIVATE)
        val edit = prefs.edit()
        if (intent.hasExtra("host")) edit.putString("host", intent.getStringExtra("host")?.trim() ?: "")
        if (intent.hasExtra("port")) { val port = intent.getIntExtra("port", 11126); if (port in 1..65535) edit.putInt("port", port) }
        edit.apply()
    }
    private fun toggle() {
        if (HeartService.running) { stopService(Intent(this, HeartService::class.java)); return }
        if (checkSelfPermission(permission) != PackageManager.PERMISSION_GRANTED) { requestPermissions(arrayOf(permission), 1); return }
        if (getSharedPreferences("destination", MODE_PRIVATE).getString("host", "").isNullOrBlank()) { update(); return }
        startForegroundService(Intent(this, HeartService::class.java))
    }
    private fun update() {
        val prefs = getSharedPreferences("destination", MODE_PRIVATE)
        val host = prefs.getString("host", "").orEmpty()
        val destination = if (host.isBlank()) "PC 주소를 설정하세요" else "$host:${prefs.getInt("port", 11126)}"
        status.text = "심박: ${HeartService.bpm?.let { "%.1f".format(it) } ?: "--"} bpm\n$destination\n패킷: ${HeartService.count}\n가속 ${HeartService.acceleration?.let { "%.1f".format(it) } ?: "--"} m/s²\n각속 ${HeartService.angularSpeed?.let { "%.1f".format(it) } ?: "--"} rad/s\n오류: ${DiagLog.lastError}"
        button.text = if (HeartService.running) "중지" else "시작"
    }
    override fun onResume() { super.onResume(); handler.post(refresh) }
    override fun onPause() { handler.removeCallbacks(refresh); super.onPause() }
    @Deprecated("Android permission callback")
    override fun onRequestPermissionsResult(requestCode: Int, permissions: Array<out String>, grantResults: IntArray) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults)
        DiagLog.event(this, "INFO", "perm", "request=$requestCode granted=${grantResults.firstOrNull() == PackageManager.PERMISSION_GRANTED}")
    }
}
