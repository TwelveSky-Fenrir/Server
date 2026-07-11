using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzUseHotkeyItemSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(8, UseHotkeyItemRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.UseHotkeyItem, UseHotkeyItemRequest.Opcode);
    }

    [Fact]
    public void TryRead_GoldenBytes_DecodesEveryField()
    {
        var golden = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 11);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(4), 22);

        Assert.True(UseHotkeyItemRequest.TryRead(golden, out var decoded));
        Assert.Equal(11, decoded.Page1);
        Assert.Equal(22, decoded.Index1);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(UseHotkeyItemRequest.TryRead(new byte[7], out _));
    }
}
