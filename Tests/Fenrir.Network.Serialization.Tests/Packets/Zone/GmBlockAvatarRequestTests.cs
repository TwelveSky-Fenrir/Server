using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

// Fenrir's dedicated GM-BLOCK command (legacy case 519 "[GM]-BLOCK", Server/ts25zone/S04_MyWork04.cpp:1487-1515).
public class GmBlockAvatarRequestTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(13, GmBlockAvatarRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.GmBlockAvatar, GmBlockAvatarRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var golden = new byte[GmBlockAvatarRequest.PayloadSize];
        WireTestKit.WriteFixedString(golden.AsSpan(0, 13), "Griefer");

        var ok = GmBlockAvatarRequest.TryRead(golden, out var packet);

        Assert.True(ok);
        Assert.Equal("Griefer", packet.AvatarName);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(GmBlockAvatarRequest.TryRead(new byte[12], out _));
    }
}
