using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzFriendFindSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, FriendLocateRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.FriendLocate, FriendLocateRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var golden = new byte[FriendLocateRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(0), 1005);

        var ok = FriendLocateRequest.TryRead(golden, out var packet);

        Assert.True(ok);
        Assert.Equal(1005, packet.Index);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(FriendLocateRequest.TryRead(new byte[3], out _));
    }
}
