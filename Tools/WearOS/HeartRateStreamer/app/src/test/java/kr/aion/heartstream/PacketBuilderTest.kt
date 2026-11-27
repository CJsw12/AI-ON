package kr.aion.heartstream

import org.junit.Assert.*
import org.junit.Test

class PacketBuilderTest {
    @Test fun imuExampleAndInvalid() {
        val result = PacketBuilder.buildImu(7, 1795650000123, .12, 9.71, 1.05, .01, -.32, .05, 11.84, .9, 5)!!
        assertEquals("{\"v\":1,\"k\":\"imu\",\"seq\":7,\"t\":1795650000123,\"a\":[0.12,9.71,1.05],\"g\":[0.01,-0.32,0.05],\"am\":11.84,\"gm\":0.9,\"n\":5}", result)
        assertFalse(result.contains('\n')); assertFalse(result.contains('\r')); assertTrue(result.length < 512)
        assertNull(PacketBuilder.buildImu(0, 0, Double.NaN, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 1))
        assertNull(PacketBuilder.buildImu(0, 0, 0.0, 0.0, 0.0, 0.0, 0.0, Double.POSITIVE_INFINITY, 0.0, 0.0, 1))
    }
    @Test fun exactExample() {
        assertEquals("{\"v\":1,\"seq\":42,\"t\":1795650000123,\"bpm\":72.0,\"ok\":true}", PacketBuilder.build(42, 1795650000123, 72.0))
    }
    @Test fun unavailable() {
        assertEquals("{\"v\":1,\"seq\":43,\"t\":1795650001123,\"ok\":false}", PacketBuilder.build(43, 1795650001123, null))
        for (value in listOf(Double.NaN, Double.POSITIVE_INFINITY, 0.0, -1.0)) assertEquals(PacketBuilder.build(1, 2, null), PacketBuilder.build(1, 2, value))
    }
    @Test fun roundedAndSingleLine() {
        val packet = PacketBuilder.build(0, 1, 72.34)
        assertEquals("{\"v\":1,\"seq\":0,\"t\":1,\"bpm\":72.3,\"ok\":true}", packet)
        assertFalse(packet.contains('\n')); assertFalse(packet.contains('\r'))
    }
}
