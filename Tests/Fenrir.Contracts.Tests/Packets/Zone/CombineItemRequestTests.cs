using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

/// <summary>CZ_ADD_ITEM_SEND (CLIENT.h:264, 20-byte payload) — same typedef as <see cref="EnchantItemRequest" />.</summary>
public class CzAddItemSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(20, CombineItemRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.CombineItem, CombineItemRequest.Opcode);
    }

    [Fact]
    public void TryRead_GoldenBytes_DecodesEveryField()
    {
        var golden = new byte[20];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 10);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(4), 20);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(8), 30);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(12), 40);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(16), 1);

        Assert.True(CombineItemRequest.TryRead(golden, out var decoded));
        Assert.Equal(10, decoded.Page1);
        Assert.Equal(20, decoded.Index1);
        Assert.Equal(30, decoded.Page2);
        Assert.Equal(40, decoded.Index2);
        Assert.Equal(1, decoded.Luck);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(CombineItemRequest.TryRead(new byte[19], out _));
    }
}
