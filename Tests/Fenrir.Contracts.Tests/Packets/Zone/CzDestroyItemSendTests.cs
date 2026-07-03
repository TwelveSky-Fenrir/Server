using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

/// <summary>CZ_DESTROY_ITEM_SEND (CLIENT.h:231, 8-byte payload) — same typedef as <see cref="CzUseHotkeyItemSend" />.</summary>
public class CzDestroyItemSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(8, CzDestroyItemSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.DestroyItemSend, CzDestroyItemSend.Opcode);
    }

    [Fact]
    public void TryRead_GoldenBytes_DecodesEveryField()
    {
        var golden = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 11);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(4), 22);

        Assert.True(CzDestroyItemSend.TryRead(golden, out var decoded));
        Assert.Equal(11, decoded.Page1);
        Assert.Equal(22, decoded.Index1);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(CzDestroyItemSend.TryRead(new byte[7], out _));
    }
}
