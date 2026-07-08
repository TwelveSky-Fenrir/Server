using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

/// <summary>CZ_USE_INVENTORY_ITEM_SEND (CLIENT.h:243-248, 12-byte payload): Page/Index/Value.</summary>
public class CzUseInventoryItemSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(12, UseInventoryItemRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.UseInventoryItem, UseInventoryItemRequest.Opcode);
    }

    [Fact]
    public void TryRead_GoldenBytes_DecodesEveryField()
    {
        var golden = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 11);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(4), 22);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(8), 33);

        Assert.True(UseInventoryItemRequest.TryRead(golden, out var decoded));
        Assert.Equal(11, decoded.Page);
        Assert.Equal(22, decoded.Index);
        Assert.Equal(33, decoded.Value);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(UseInventoryItemRequest.TryRead(new byte[11], out _));
    }
}
