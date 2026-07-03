using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

/// <summary>
///     ZC_USE_INVENTORY_ITEM_RECV (ZONE.h:484-493, 20-byte payload): Result/Page/Index/Value/Value2
///     (USE_PREMIUM_LONGTIME active in EU33 — 5 ints, not 4).
/// </summary>
public class ZcUseInventoryItemRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(20, UseInventoryItemResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.UseInventoryItem, UseInventoryItemResponse.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = new UseInventoryItemResponse { Result = 11, Page = 22, Index = 33, Value = 44, Value2 = 55 };

        var actual = new byte[UseInventoryItemResponse.PayloadSize];
        value.Write(actual);

        var expected = new byte[20];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 11);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(4), 22);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(8), 33);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(12), 44);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(16), 55);

        Assert.Equal(expected, actual);
    }
}
