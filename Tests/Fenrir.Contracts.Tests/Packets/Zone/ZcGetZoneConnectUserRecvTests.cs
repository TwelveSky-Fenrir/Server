using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcGetZoneConnectUserRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(8, ZcGetZoneConnectUserRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.GetZoneConnectUserRecv, ZcGetZoneConnectUserRecv.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = new ZcGetZoneConnectUserRecv { ZoneNumber = 349, ConnectedUser = 12 };

        var actual = new byte[8];
        value.Write(actual);

        var expected = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 349);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(4), 12);

        Assert.Equal(expected, actual);
    }
}
