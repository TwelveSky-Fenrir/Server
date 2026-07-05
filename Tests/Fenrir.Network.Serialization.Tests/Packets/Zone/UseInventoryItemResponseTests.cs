using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

/// <summary>ZC_USE_INVENTORY_ITEM_RECV (ZONE.h:484-493): 5 ints, not 4 — USE_PREMIUM_LONGTIME is active in EU33.</summary>
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
