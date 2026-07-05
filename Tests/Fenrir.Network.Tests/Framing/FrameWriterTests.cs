using Fenrir.Network.Serialization.Packets.Login;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Framing;

namespace Fenrir.Network.Tests.Framing;

public class FrameWriterTests
{
    [Fact]
    public void FrameSizeOf_ZcConnectOkRecv_IsHeaderPlusPayload()
    {
        Assert.Equal(1 + ZoneGreetingResponse.PayloadSize, FrameWriter.FrameSizeOf<ZoneGreetingResponse>());
    }

    [Fact]
    public void FrameSizeOf_ZcTempRegisterRecv_IsHeaderPlusPayload()
    {
        Assert.Equal(1 + ZoneHandshakeResponse.PayloadSize, FrameWriter.FrameSizeOf<ZoneHandshakeResponse>());
    }

    [Fact]
    public void FrameSizeOf_LcLoginConnectOkRecv_IsHeaderPlusPayload()
    {
        Assert.Equal(1 + LoginGreetingResponse.PayloadSize, FrameWriter.FrameSizeOf<LoginGreetingResponse>());
    }

    [Fact]
    public void WriteFrame_NoObfuscation_ZcConnectOkRecv_HeaderIsRawOpcodeAndPayloadMatchesManualWrite()
    {
        var packet = new ZoneGreetingResponse { RandomNumber = 0x12345678 };

        Span<byte> destination = stackalloc byte[FrameWriter.FrameSizeOf<ZoneGreetingResponse>()];
        var written = FrameWriter.WriteFrame(packet, destination);

        Assert.Equal(destination.Length, written);
        Assert.Equal(FrameWriter.FrameSizeOf<ZoneGreetingResponse>(), written);
        Assert.Equal(1 + ZoneGreetingResponse.PayloadSize, written);

        // Undeclared Obfuscation -> WriteFrame never enters the XOR branch.
        Assert.Equal(ZoneGreetingResponse.Opcode, destination[0]);

        Span<byte> expectedPayload = stackalloc byte[ZoneGreetingResponse.PayloadSize];
        packet.Write(expectedPayload);
        Assert.True(expectedPayload.SequenceEqual(destination[1..]));
    }

    [Fact]
    public void WriteFrame_NoObfuscation_ZcTempRegisterRecv_HeaderIsRawOpcodeAndPayloadMatchesManualWrite()
    {
        var packet = new ZoneHandshakeResponse { Result = 7 };

        Span<byte> destination = stackalloc byte[FrameWriter.FrameSizeOf<ZoneHandshakeResponse>()];
        var written = FrameWriter.WriteFrame(packet, destination);

        Assert.Equal(destination.Length, written);
        Assert.Equal(FrameWriter.FrameSizeOf<ZoneHandshakeResponse>(), written);
        Assert.Equal(1 + ZoneHandshakeResponse.PayloadSize, written);
        Assert.Equal(ZoneHandshakeResponse.Opcode, destination[0]);

        Span<byte> expectedPayload = stackalloc byte[ZoneHandshakeResponse.PayloadSize];
        packet.Write(expectedPayload);
        Assert.True(expectedPayload.SequenceEqual(destination[1..]));
    }

    [Fact]
    public void WriteFrame_XorPacketGlobal_LcLoginConnectOkRecv_AppliesWholeFrameXorAndSparesLastByte()
    {
        var packet = new LoginGreetingResponse
        {
            RandomNumber = 0x11223344,
            MaxPlayerNum = 500,
            GagePlayerNum = 42,
            PresentPlayerNum = 7
        };

        Span<byte> destination = stackalloc byte[FrameWriter.FrameSizeOf<LoginGreetingResponse>()];
        var written = FrameWriter.WriteFrame(packet, destination);

        Assert.Equal(destination.Length, written);
        Assert.Equal(FrameWriter.FrameSizeOf<LoginGreetingResponse>(), written);
        Assert.Equal(1 + LoginGreetingResponse.PayloadSize, written);

        // ApplyPacketXor: buf[0] ^= 0x10.
        Assert.Equal((byte)(LoginGreetingResponse.Opcode ^ 0x10), destination[0]);

        Span<byte> rawPayload = stackalloc byte[LoginGreetingResponse.PayloadSize];
        packet.Write(rawPayload);

        // Last byte is spared by ApplyPacketXor.
        Assert.Equal(rawPayload[^1], destination[^1]);

        // Interior bytes carry the steady 0xFE key on top of the raw Write() bytes.
        for (var i = 0; i < rawPayload.Length - 1; i++)
            Assert.Equal((byte)(rawPayload[i] ^ 0xFE), destination[i + 1]);
    }
}
