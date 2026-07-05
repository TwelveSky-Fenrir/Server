using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcTribeVoteRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(12, TribeVoteResponse.PayloadSize);
        Assert.Equal(4 + 4 + 4, TribeVoteResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.TribeVote, TribeVoteResponse.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new TribeVoteResponse { Result = 0, Sort = 3, Value = 6 };

        Span<byte> buffer = stackalloc byte[TribeVoteResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(TribeVoteResponse.PayloadSize, written);

        Span<byte> golden = stackalloc byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 0);
        BinaryPrimitives.WriteInt32LittleEndian(golden[4..], 3);
        BinaryPrimitives.WriteInt32LittleEndian(golden[8..], 6);

        Assert.True(golden.SequenceEqual(buffer));
    }
}
