using Fenrir.Contracts.Packets.Login;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Framing;

namespace Fenrir.Network.Tests.Framing;

public class FrameWriterTests
{
    [Fact]
    public void FrameSizeOf_ZcConnectOkRecv_IsHeaderPlusPayload()
    {
        Assert.Equal(1 + ZcConnectOkRecv.PayloadSize, FrameWriter.FrameSizeOf<ZcConnectOkRecv>());
    }

    [Fact]
    public void FrameSizeOf_ZcTempRegisterRecv_IsHeaderPlusPayload()
    {
        Assert.Equal(1 + ZcTempRegisterRecv.PayloadSize, FrameWriter.FrameSizeOf<ZcTempRegisterRecv>());
    }

    [Fact]
    public void FrameSizeOf_LcLoginConnectOkRecv_IsHeaderPlusPayload()
    {
        Assert.Equal(1 + LcLoginConnectOkRecv.PayloadSize, FrameWriter.FrameSizeOf<LcLoginConnectOkRecv>());
    }

    [Fact]
    public void WriteFrame_NoObfuscation_ZcConnectOkRecv_HeaderIsRawOpcodeAndPayloadMatchesManualWrite()
    {
        var packet = new ZcConnectOkRecv { RandomNumber = 0x12345678 };

        Span<byte> destination = stackalloc byte[FrameWriter.FrameSizeOf<ZcConnectOkRecv>()];
        var written = FrameWriter.WriteFrame(packet, destination);

        Assert.Equal(destination.Length, written);
        Assert.Equal(FrameWriter.FrameSizeOf<ZcConnectOkRecv>(), written);
        Assert.Equal(1 + ZcConnectOkRecv.PayloadSize, written);

        // Undeclared Obfuscation -> WriteFrame never enters the XOR branch, so destination[0] is left in
        // whatever state it was written in (the "before xor" state, since there is no "after" for this packet).
        Assert.Equal(ZcConnectOkRecv.Opcode, destination[0]);

        Span<byte> expectedPayload = stackalloc byte[ZcConnectOkRecv.PayloadSize];
        packet.Write(expectedPayload);
        Assert.True(expectedPayload.SequenceEqual(destination[1..]));
    }

    [Fact]
    public void WriteFrame_NoObfuscation_ZcTempRegisterRecv_HeaderIsRawOpcodeAndPayloadMatchesManualWrite()
    {
        var packet = new ZcTempRegisterRecv { Result = 7 };

        Span<byte> destination = stackalloc byte[FrameWriter.FrameSizeOf<ZcTempRegisterRecv>()];
        var written = FrameWriter.WriteFrame(packet, destination);

        Assert.Equal(destination.Length, written);
        Assert.Equal(FrameWriter.FrameSizeOf<ZcTempRegisterRecv>(), written);
        Assert.Equal(1 + ZcTempRegisterRecv.PayloadSize, written);
        Assert.Equal(ZcTempRegisterRecv.Opcode, destination[0]);

        Span<byte> expectedPayload = stackalloc byte[ZcTempRegisterRecv.PayloadSize];
        packet.Write(expectedPayload);
        Assert.True(expectedPayload.SequenceEqual(destination[1..]));
    }

    [Fact]
    public void WriteFrame_XorPacketGlobal_LcLoginConnectOkRecv_AppliesWholeFrameXorAndSparesLastByte()
    {
        var packet = new LcLoginConnectOkRecv
        {
            RandomNumber = 0x11223344,
            MaxPlayerNum = 500,
            GagePlayerNum = 42,
            PresentPlayerNum = 7
        };

        Span<byte> destination = stackalloc byte[FrameWriter.FrameSizeOf<LcLoginConnectOkRecv>()];
        var written = FrameWriter.WriteFrame(packet, destination);

        Assert.Equal(destination.Length, written);
        Assert.Equal(FrameWriter.FrameSizeOf<LcLoginConnectOkRecv>(), written);
        Assert.Equal(1 + LcLoginConnectOkRecv.PayloadSize, written);

        // ApplyPacketXor (§3.1): buf[0] ^= 0x10 -> the header byte moves from the raw opcode to opcode^0x10.
        Assert.Equal((byte)(LcLoginConnectOkRecv.Opcode ^ 0x10), destination[0]);

        Span<byte> rawPayload = stackalloc byte[LcLoginConnectOkRecv.PayloadSize];
        packet.Write(rawPayload);

        // buf[size-1] is spared by ApplyPacketXor -> the frame's last byte still equals Write's raw output.
        Assert.Equal(rawPayload[^1], destination[^1]);

        // Remaining interior bytes (payload[0..^1]) carry the steady 0xFE key on top of the raw Write() bytes.
        for (var i = 0; i < rawPayload.Length - 1; i++)
            Assert.Equal((byte)(rawPayload[i] ^ 0xFE), destination[i + 1]);
    }
}
