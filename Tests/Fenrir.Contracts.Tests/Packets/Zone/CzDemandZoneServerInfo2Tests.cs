using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzDemandZoneServerInfo2Tests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(12, CzDemandZoneServerInfo2.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.DemandZoneServerInfo2, CzDemandZoneServerInfo2.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        Span<byte> buffer = new byte[CzDemandZoneServerInfo2.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 4);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[4..], 120);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[8..], 55);

        var ok = CzDemandZoneServerInfo2.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(4, packet.Sort);
        Assert.Equal(120, packet.ZoneNumber);
        Assert.Equal(55, packet.PresentZoneNumber);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(CzDemandZoneServerInfo2.TryRead(new byte[11], out _));
    }
}
