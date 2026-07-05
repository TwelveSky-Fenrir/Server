using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

/// <summary>CZ_DESTROY_ITEM_SEND (CLIENT.h:231, 8-byte payload) — same typedef as <see cref="UseHotkeyItemRequest" />.</summary>
public class CzDestroyItemSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(8, DestroyItemRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.DestroyItem, DestroyItemRequest.Opcode);
    }

    [Fact]
    public void TryRead_GoldenBytes_DecodesEveryField()
    {
        var golden = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 11);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(4), 22);

        Assert.True(DestroyItemRequest.TryRead(golden, out var decoded));
        Assert.Equal(11, decoded.Page1);
        Assert.Equal(22, decoded.Index1);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(DestroyItemRequest.TryRead(new byte[7], out _));
    }
}
