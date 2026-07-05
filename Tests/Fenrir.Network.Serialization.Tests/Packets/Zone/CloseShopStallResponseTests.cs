using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcEndPshopRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, CloseShopStallResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.CloseShopStall, CloseShopStallResponse.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new CloseShopStallResponse { Result = 1 };

        var actual = new byte[CloseShopStallResponse.PayloadSize];
        var written = packet.Write(actual);

        Assert.Equal(CloseShopStallResponse.PayloadSize, written);

        var expected = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 1);

        Assert.Equal(expected, actual);
    }
}
