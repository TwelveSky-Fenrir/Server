using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcTribeAllianceInfoTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(8, ZcTribeAllianceInfo.PayloadSize);
        Assert.Equal(4 + 4, ZcTribeAllianceInfo.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.TribeAllianceInfo, ZcTribeAllianceInfo.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new ZcTribeAllianceInfo { Sort = 4, Value = 120 };

        Span<byte> buffer = stackalloc byte[ZcTribeAllianceInfo.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ZcTribeAllianceInfo.PayloadSize, written);

        Span<byte> golden = stackalloc byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 4);
        BinaryPrimitives.WriteInt32LittleEndian(golden[4..], 120);

        Assert.True(golden.SequenceEqual(buffer));
    }
}
