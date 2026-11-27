package kr.aion.heartstream

import org.junit.Assert.*
import org.junit.Test

class ImuBufferTest {
    @Test fun averagesPeaksAndReset() {
        val buffer = ImuBuffer()
        assertNull(buffer.drain())
        buffer.add(true, 3.0, 4.0, 0.0)
        buffer.add(true, 0.0, 0.0, 0.0)
        buffer.add(false, 0.0, 0.0, 2.0)
        val sample = buffer.drain()!!
        assertEquals(3, sample.n); assertEquals(1.5, sample.a[0], 0.0); assertEquals(2.0, sample.a[1], 0.0)
        assertEquals(5.0, sample.am, 0.0); assertEquals(2.0, sample.gm, 0.0); assertEquals(2.0, sample.g[2], 0.0)
        assertNull(buffer.drain())
    }
}
