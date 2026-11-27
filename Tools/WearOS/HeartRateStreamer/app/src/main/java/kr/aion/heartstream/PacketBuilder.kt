package kr.aion.heartstream

import java.util.Locale

object PacketBuilder {
    fun buildImu(seq: Long, tMillis: Long, ax: Double, ay: Double, az: Double, gx: Double, gy: Double, gz: Double, am: Double, gm: Double, n: Int): String? {
        val values = listOf(ax, ay, az, gx, gy, gz, am, gm)
        if (values.any { !it.isFinite() }) return null
        fun number(value: Double) = String.format(Locale.US, "%.2f", value).trimEnd('0').trimEnd('.') .let { if (it == "-0") "0" else it }
        return "{\"v\":1,\"k\":\"imu\",\"seq\":$seq,\"t\":$tMillis,\"a\":[${number(ax)},${number(ay)},${number(az)}],\"g\":[${number(gx)},${number(gy)},${number(gz)}],\"am\":${number(am)},\"gm\":${number(gm)},\"n\":$n}".takeIf { it.length < 512 }
    }
    fun build(seq: Long, tMillis: Long, bpm: Double?): String {
        val prefix = "{\"v\":1,\"seq\":$seq,\"t\":$tMillis"
        return if (bpm != null && bpm.isFinite() && bpm > 0.0) {
            "$prefix,\"bpm\":${String.format(Locale.US, "%.1f", bpm)},\"ok\":true}"
        } else "$prefix,\"ok\":false}"
    }
}
