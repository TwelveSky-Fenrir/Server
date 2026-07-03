using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

/// <summary>ZC_LOW_ITEM_RECV (ZONE.h:531, 32-byte payload) — same typedef as <see cref="ZcExchangeItemRecv" />.</summary>
public class ZcLowItemRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(32, ZcLowItemRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.LowItemRecv, ZcLowItemRecv.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[ZcLowItemRecv.PayloadSize];
        value.Write(actual);

        var expected = new byte[32];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    private static ZcLowItemRecv CreatePopulated()
    {
        return new ZcLowItemRecv { Result = 11, Cost = 22, Value = [100, 101, 102, 103, 104, 105] };
    }

    private static void EncodeGolden(Span<byte> destination, ZcLowItemRecv value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Result);
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], value.Cost);
        for (var i = 0; i < 6; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(8 + i * 4)..], value.Value[i]);
    }
}
