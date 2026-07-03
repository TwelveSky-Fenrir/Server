using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

/// <summary>CZ_UP_LEVEL_ITEM_SEND (CLIENT.h:271, 16-byte payload) — same typedef as <see cref="CzSkyUpItemSend" />.</summary>
public class CzUpLevelItemSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(16, CzUpLevelItemSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.UpLevelItemSend, CzUpLevelItemSend.Opcode);
    }

    [Fact]
    public void TryRead_GoldenBytes_DecodesEveryField()
    {
        var golden = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 11);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(4), 22);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(8), 33);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(12), 44);

        Assert.True(CzUpLevelItemSend.TryRead(golden, out var decoded));
        Assert.Equal(11, decoded.Page1);
        Assert.Equal(22, decoded.Index1);
        Assert.Equal(33, decoded.Page2);
        Assert.Equal(44, decoded.Index2);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(CzUpLevelItemSend.TryRead(new byte[15], out _));
    }
}
