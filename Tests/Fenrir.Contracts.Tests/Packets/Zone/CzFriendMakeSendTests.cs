using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzFriendMakeSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, CzFriendMakeSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.FriendMakeSend, CzFriendMakeSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var golden = new byte[CzFriendMakeSend.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(0), 1005);

        var ok = CzFriendMakeSend.TryRead(golden, out var packet);

        Assert.True(ok);
        Assert.Equal(1005, packet.Index);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(CzFriendMakeSend.TryRead(new byte[3], out _));
    }
}
