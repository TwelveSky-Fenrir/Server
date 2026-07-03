using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcTribeVoteRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(12, ZcTribeVoteRecv.PayloadSize);
        Assert.Equal(4 + 4 + 4, ZcTribeVoteRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.TribeVoteRecv, ZcTribeVoteRecv.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new ZcTribeVoteRecv { Result = 0, Sort = 3, Value = 6 };

        Span<byte> buffer = stackalloc byte[ZcTribeVoteRecv.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ZcTribeVoteRecv.PayloadSize, written);

        Span<byte> golden = stackalloc byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 0);
        BinaryPrimitives.WriteInt32LittleEndian(golden[4..], 3);
        BinaryPrimitives.WriteInt32LittleEndian(golden[8..], 6);

        Assert.True(golden.SequenceEqual(buffer));
    }
}
