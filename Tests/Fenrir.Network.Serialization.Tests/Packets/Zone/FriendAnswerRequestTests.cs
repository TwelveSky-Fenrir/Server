using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzFriendAnswerSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, FriendAnswerRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.FriendAnswer, FriendAnswerRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var golden = new byte[FriendAnswerRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(0), 1006);

        var ok = FriendAnswerRequest.TryRead(golden, out var packet);

        Assert.True(ok);
        Assert.Equal(1006, packet.Answer);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(FriendAnswerRequest.TryRead(new byte[3], out _));
    }
}
