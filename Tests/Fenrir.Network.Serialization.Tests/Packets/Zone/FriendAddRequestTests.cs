using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzFriendMakeSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, FriendAddRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.FriendAdd, FriendAddRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var golden = new byte[FriendAddRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(0), 1005);

        var ok = FriendAddRequest.TryRead(golden, out var packet);

        Assert.True(ok);
        Assert.Equal(1005, packet.Index);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(FriendAddRequest.TryRead(new byte[3], out _));
    }
}
