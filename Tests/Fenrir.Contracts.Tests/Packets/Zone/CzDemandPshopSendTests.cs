using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzDemandPshopSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(13, CzDemandPshopSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.DemandPshopSend, CzDemandPshopSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[CzDemandPshopSend.PayloadSize];
        WireTestKit.WriteFixedString(buffer, "Odin");

        var ok = CzDemandPshopSend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal("Odin", packet.AvatarName);
    }
}
