using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

/// <summary>CZ_HIGH_ITEM_SEND (CLIENT.h:264, 20-byte payload) — same typedef as <see cref="EnchantItemRequest" />.</summary>
public class CzHighItemSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(20, UpgradeItemRankRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.UpgradeItemRank, UpgradeItemRankRequest.Opcode);
    }

    [Fact]
    public void TryRead_GoldenBytes_DecodesEveryField()
    {
        var golden = new byte[20];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 11);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(4), 22);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(8), 33);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(12), 44);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(16), 55);

        Assert.True(UpgradeItemRankRequest.TryRead(golden, out var decoded));
        Assert.Equal(11, decoded.Page1);
        Assert.Equal(22, decoded.Index1);
        Assert.Equal(33, decoded.Page2);
        Assert.Equal(44, decoded.Index2);
        Assert.Equal(55, decoded.Luck);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(UpgradeItemRankRequest.TryRead(new byte[19], out _));
    }
}
