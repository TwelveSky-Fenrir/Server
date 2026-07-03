using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzGetZoneConnectUserSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, CzGetZoneConnectUserSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.GetZoneConnectUserSend, CzGetZoneConnectUserSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[CzGetZoneConnectUserSend.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 349);

        var ok = CzGetZoneConnectUserSend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(349, packet.ZoneNumber);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(CzGetZoneConnectUserSend.TryRead(new byte[3], out _));
    }
}
