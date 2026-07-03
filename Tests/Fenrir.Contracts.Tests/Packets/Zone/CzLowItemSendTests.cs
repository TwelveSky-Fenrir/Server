using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

/// <summary>CZ_LOW_ITEM_SEND (CLIENT.h:264, 20-byte payload) — same typedef as <see cref="CzImproveItemSend" />.</summary>
public class CzLowItemSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(20, CzLowItemSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.LowItemSend, CzLowItemSend.Opcode);
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

        Assert.True(CzLowItemSend.TryRead(golden, out var decoded));
        Assert.Equal(11, decoded.Page1);
        Assert.Equal(22, decoded.Index1);
        Assert.Equal(33, decoded.Page2);
        Assert.Equal(44, decoded.Index2);
        Assert.Equal(55, decoded.Luck);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(CzLowItemSend.TryRead(new byte[19], out _));
    }
}
