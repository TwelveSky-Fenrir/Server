using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcEndPshopRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, ZcEndPshopRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.EndPshopRecv, ZcEndPshopRecv.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new ZcEndPshopRecv { Result = 1 };

        var actual = new byte[ZcEndPshopRecv.PayloadSize];
        var written = packet.Write(actual);

        Assert.Equal(ZcEndPshopRecv.PayloadSize, written);

        var expected = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 1);

        Assert.Equal(expected, actual);
    }
}
