using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcTribeAllianceInfoTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(8, TribeAllianceStatusResponse.PayloadSize);
        Assert.Equal(4 + 4, TribeAllianceStatusResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.TribeAllianceStatus, TribeAllianceStatusResponse.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new TribeAllianceStatusResponse { Sort = 4, Value = 120 };

        Span<byte> buffer = stackalloc byte[TribeAllianceStatusResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(TribeAllianceStatusResponse.PayloadSize, written);

        Span<byte> golden = stackalloc byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 4);
        BinaryPrimitives.WriteInt32LittleEndian(golden[4..], 120);

        Assert.True(golden.SequenceEqual(buffer));
    }
}
