using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzFriendFindSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, CzFriendFindSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.FriendFindSend, CzFriendFindSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var golden = new byte[CzFriendFindSend.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(0), 1005);

        var ok = CzFriendFindSend.TryRead(golden, out var packet);

        Assert.True(ok);
        Assert.Equal(1005, packet.Index);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(CzFriendFindSend.TryRead(new byte[3], out _));
    }
}
