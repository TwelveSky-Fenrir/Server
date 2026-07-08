using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzTribeVoteSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(8, TribeVoteRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.TribeVote, TribeVoteRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[TribeVoteRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 1);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[4..], 4);

        var ok = TribeVoteRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(1, packet.Sort);
        Assert.Equal(4, packet.Value);
    }
}
