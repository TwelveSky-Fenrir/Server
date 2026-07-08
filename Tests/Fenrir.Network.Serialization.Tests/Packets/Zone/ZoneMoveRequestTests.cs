using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzDemandZoneServerInfo2Tests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(12, ZoneMoveRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.ZoneMove, ZoneMoveRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        Span<byte> buffer = new byte[ZoneMoveRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 4);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[4..], 120);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[8..], 55);

        var ok = ZoneMoveRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(4, packet.Sort);
        Assert.Equal(120, packet.ZoneNumber);
        Assert.Equal(55, packet.PresentZoneNumber);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(ZoneMoveRequest.TryRead(new byte[11], out _));
    }
}
