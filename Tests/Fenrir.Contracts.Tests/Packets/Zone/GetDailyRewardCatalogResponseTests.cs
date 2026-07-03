using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcGetRewardItemRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(32, GetDailyRewardCatalogResponse.PayloadSize);
        Assert.Equal(28 + 4, GetDailyRewardCatalogResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.GetDailyRewardCatalog, GetDailyRewardCatalogResponse.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var rewardItem = new[] { 100, 101, 102, 103, 104, 105, 106 };
        var packet = new GetDailyRewardCatalogResponse { RewardItem = rewardItem, RewardDay = 3 };

        var actual = new byte[GetDailyRewardCatalogResponse.PayloadSize];
        var written = packet.Write(actual);
        Assert.Equal(GetDailyRewardCatalogResponse.PayloadSize, written);

        var expected = new byte[GetDailyRewardCatalogResponse.PayloadSize];
        for (var i = 0; i < rewardItem.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(i * 4), rewardItem[i]);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(28), 3);

        Assert.Equal(expected, actual);
    }
}
