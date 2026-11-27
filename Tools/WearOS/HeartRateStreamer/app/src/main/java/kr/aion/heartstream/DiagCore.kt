package kr.aion.heartstream

object DiagCore {
    const val LIMIT = 512 * 1024L
    fun line(time: Long, level: String, event: String, fields: String): String {
        fun clean(s: String) = s.replace(Regex("[\\r\\n\\t]+"), " ")
        return "$time\t${clean(level)}\t${clean(event)}\t${clean(fields)}\n"
    }
    fun rotate(size: Long, incoming: Int) = size + incoming > LIMIT
}

class ErrorLimiter {
    private val last = mutableMapOf<String, Long>()
    fun allow(key: String, now: Long): Boolean {
        val previous = last[key]
        if (previous != null && now - previous < 10_000) return false
        last[key] = now
        return true
    }
}
