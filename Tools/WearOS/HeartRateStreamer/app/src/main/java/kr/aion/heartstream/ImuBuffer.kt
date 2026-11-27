package kr.aion.heartstream

import kotlin.math.sqrt

class ImuBuffer {
    private var accel = DoubleArray(3)
    private var gyro = DoubleArray(3)
    private var accelCount = 0
    private var gyroCount = 0
    private var accelMax = 0.0
    private var gyroMax = 0.0
    @Synchronized fun add(accelerometer: Boolean, x: Double, y: Double, z: Double) {
        if (!x.isFinite() || !y.isFinite() || !z.isFinite()) return
        val sum = if (accelerometer) accel else gyro
        sum[0] += x; sum[1] += y; sum[2] += z
        val magnitude = sqrt(x*x + y*y + z*z)
        if (accelerometer) { accelCount++; accelMax = maxOf(accelMax, magnitude) }
        else { gyroCount++; gyroMax = maxOf(gyroMax, magnitude) }
    }
    @Synchronized fun drain(): Sample? {
        val n = accelCount + gyroCount
        if (n == 0) return null
        val result = Sample(accel.map { it / maxOf(1, accelCount) }, gyro.map { it / maxOf(1, gyroCount) }, accelMax, gyroMax, n)
        accel = DoubleArray(3); gyro = DoubleArray(3); accelCount = 0; gyroCount = 0; accelMax = 0.0; gyroMax = 0.0
        return result
    }
    data class Sample(val a: List<Double>, val g: List<Double>, val am: Double, val gm: Double, val n: Int)
}
