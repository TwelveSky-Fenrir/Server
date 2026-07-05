using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcTempRegisterRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, ZoneHandshakeResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.ZoneHandshake, ZoneHandshakeResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = new ZoneHandshakeResponse { Result = 1 };

        Span<byte> buffer = stackalloc byte[ZoneHandshakeResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ZoneHandshakeResponse.PayloadSize, written);
        Assert.Equal(packet.Result, BinaryPrimitives.ReadInt32LittleEndian(buffer));
    }
}
