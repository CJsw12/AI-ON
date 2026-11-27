package kr.aion.heartstream

import android.content.Context
import android.util.Log
import java.io.File
import java.util.concurrent.Executors

object DiagLog {
    private val writer = Executors.newSingleThreadExecutor()
    @Volatile var lastError = "정상"
        private set
    fun event(context: Context, level: String, event: String, fields: String = "") {
        if (level == "WARN" || level == "ERROR") lastError = event
        val directory = context.applicationContext.filesDir
        val line = DiagCore.line(System.currentTimeMillis(), level, event, fields).toByteArray(Charsets.UTF_8)
        writer.execute {
            try {
                val file = File(directory, "diag.log")
                if (DiagCore.rotate(file.length(), line.size)) {
                    val previous = File(directory, "diag.log.1")
                    if (previous.exists() && !previous.delete()) error("Cannot remove old log")
                    if (file.exists() && !file.renameTo(previous)) error("Cannot rotate log")
                }
                file.appendBytes(line)
            } catch (e: Exception) { Log.w("HeartStream", "Diagnostic write failed", e) }
        }
    }
}
