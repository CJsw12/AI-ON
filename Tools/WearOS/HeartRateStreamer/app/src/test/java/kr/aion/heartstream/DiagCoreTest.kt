package kr.aion.heartstream

import org.junit.Assert.*
import org.junit.Test

class DiagCoreTest {
    @Test fun lineFormat() { assertEquals("123\tWARN\tsend_fail\tmessage=x y\n", DiagCore.line(123, "WARN", "send_fail", "message=x\ny")) }
    @Test fun rotation() { assertFalse(DiagCore.rotate(524280, 8)); assertTrue(DiagCore.rotate(524280, 9)) }
    @Test fun throttle() {
        val limiter = ErrorLimiter()
        assertTrue(limiter.allow("a", 0)); assertFalse(limiter.allow("a", 9999)); assertTrue(limiter.allow("b", 9999)); assertTrue(limiter.allow("a", 10000))
    }
}
