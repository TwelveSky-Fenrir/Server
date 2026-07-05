using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzDemandPshopSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(13, ViewShopStallRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.ViewShopStall, ViewShopStallRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[ViewShopStallRequest.PayloadSize];
        WireTestKit.WriteFixedString(buffer, "Odin");

        var ok = ViewShopStallRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal("Odin", packet.AvatarName);
    }
}
