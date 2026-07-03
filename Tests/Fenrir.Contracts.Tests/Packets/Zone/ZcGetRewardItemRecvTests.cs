using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcGetRewardItemRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(32, ZcGetRewardItemRecv.PayloadSize);
        Assert.Equal(28 + 4, ZcGetRewardItemRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.GetRewardItemRecv, ZcGetRewardItemRecv.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var rewardItem = new[] { 100, 101, 102, 103, 104, 105, 106 };
        var packet = new ZcGetRewardItemRecv { RewardItem = rewardItem, RewardDay = 3 };

        var actual = new byte[ZcGetRewardItemRecv.PayloadSize];
        var written = packet.Write(actual);
        Assert.Equal(ZcGetRewardItemRecv.PayloadSize, written);

        var expected = new byte[ZcGetRewardItemRecv.PayloadSize];
        for (var i = 0; i < rewardItem.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(i * 4), rewardItem[i]);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(28), 3);

        Assert.Equal(expected, actual);
    }
}
