using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

/// <summary>
///     ZC_SET_INVENTORY_ITEM_RECV (ZONE.h:1029-1034, 32-byte payload): Page/Index/Value[6] — pure state
///     push, no <c>Result</c> field.
/// </summary>
public class ZcSetInventoryItemRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(32, ZcSetInventoryItemRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.SetInventoryItemRecv, ZcSetInventoryItemRecv.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[ZcSetInventoryItemRecv.PayloadSize];
        value.Write(actual);

        var expected = new byte[32];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    private static ZcSetInventoryItemRecv CreatePopulated()
    {
        return new ZcSetInventoryItemRecv { Page = 11, Index = 22, Value = [100, 101, 102, 103, 104, 105] };
    }

    private static void EncodeGolden(Span<byte> destination, ZcSetInventoryItemRecv value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.Page);
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], value.Index);
        for (var i = 0; i < 6; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(8 + i * 4)..], value.Value[i]);
    }
}
