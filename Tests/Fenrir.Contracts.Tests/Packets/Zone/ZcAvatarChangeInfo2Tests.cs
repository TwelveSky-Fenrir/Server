using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcAvatarChangeInfo2Tests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        // USE_PREMIUM_LONGTIME is ON in EU33: Value2 is present -> 12 bytes (not 8).
        Assert.Equal(12, ZcAvatarChangeInfo2.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.AvatarChangeInfo2, ZcAvatarChangeInfo2.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = new ZcAvatarChangeInfo2 { Sort = 10, Value = 4800, Value2 = 0 };

        Span<byte> buffer = new byte[ZcAvatarChangeInfo2.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ZcAvatarChangeInfo2.PayloadSize, written);
        Assert.Equal(10, BinaryPrimitives.ReadInt32LittleEndian(buffer));
        Assert.Equal(4800, BinaryPrimitives.ReadInt32LittleEndian(buffer[4..]));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(buffer[8..]));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new ZcAvatarChangeInfo2 { Sort = 19, Value = 50_000, Value2 = 7 };

        var golden = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 19);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(4), 50_000);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(8), 7);

        Span<byte> buffer = new byte[ZcAvatarChangeInfo2.PayloadSize];
        packet.Write(buffer);

        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }
}
