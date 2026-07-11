using Fenrir.Application.Game.Domain.Mounts;
using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Tests.Mounts;

public class MountCatalogTests
{
    [Theory]
    [InlineData(559, true)]
    [InlineData(1332, true)]
    [InlineData(1341, true)]
    [InlineData(1342, false)]
    [InlineData(19001, false)]
    [InlineData(19002, true)]
    [InlineData(19011, true)]
    [InlineData(19012, false)]
    public void IsRecognizedMount_AcceptsContractTranscribedRanges(int itemId, bool expected)
    {
        Assert.Equal(expected, MountCatalog.IsRecognizedMount(itemId));
    }

    [Theory]
    [InlineData(8300, false)]
    [InlineData(8301, true)]
    [InlineData(8331, true)]
    [InlineData(8332, false)]
    public void IsGiftEventMount_CoversTheLiveGiftEventRange(int itemId, bool expected)
    {
        Assert.Equal(expected, MountCatalog.IsGiftEventMount(itemId));
        Assert.Equal(expected, MountCatalog.IsRecognizedMount(itemId));
    }

    [Theory]
    [InlineData(1306, false)]
    [InlineData(1307, true)]
    [InlineData(1308, true)]
    [InlineData(1309, true)]
    [InlineData(1310, false)]
    [InlineData(1315, true)]
    [InlineData(1319, true)]
    [InlineData(1322, true)]
    [InlineData(1325, true)]
    [InlineData(1328, true)]
    [InlineData(1329, false)]
    public void IsTier3Mount_AcceptsExactlyTheEightTier3Ids(int itemId, bool expected)
    {
        Assert.Equal(expected, MountCatalog.IsTier3Mount(itemId));

        Assert.Equal(expected, MountCatalog.IsRecognizedMount(itemId));
    }

    [Fact]
    public void IsTier3Mount_SetMatchesTheIndependentlyRecoveredMountBox635Pool()
    {
        foreach (var rewardItemId in MountBox635RewardTable.RewardItemIds)
            Assert.True(MountCatalog.IsTier3Mount(rewardItemId));
    }

    [Fact]
    public void IsRecognizedMount_RecognizesPuma3ViaTheSecondFallThroughStep()
    {
        Assert.Equal(1331, MountCatalog.Puma3Id);
        Assert.False(MountCatalog.IsTier3Mount(MountCatalog.Puma3Id));
        Assert.True(MountCatalog.IsRecognizedMount(MountCatalog.Puma3Id));
    }

    [Fact]
    public void IsRecognizedMount_RemainingTierMemberIdsAreNotRecognizedYet()
    {
        Assert.False(MountCatalog.IsRecognizedMount(1301));
    }
}
