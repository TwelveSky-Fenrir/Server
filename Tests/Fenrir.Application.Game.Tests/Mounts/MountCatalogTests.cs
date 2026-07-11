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
        // GIFT_EVENT is defined unconditionally -> the 8301-8331 range IS valid in production.
        Assert.Equal(expected, MountCatalog.IsGiftEventMount(itemId));
        Assert.Equal(expected, MountCatalog.IsRecognizedMount(itemId));
    }

    [Theory]
    [InlineData(1306, false)]
    [InlineData(1307, true)] // Tiger3
    [InlineData(1308, true)] // Pig3
    [InlineData(1309, true)] // Deer3
    [InlineData(1310, false)]
    [InlineData(1315, true)] // Bear3
    [InlineData(1319, true)] // Cat3
    [InlineData(1322, true)] // Bull3
    [InlineData(1325, true)] // Wolf3
    [InlineData(1328, true)] // Lion3
    [InlineData(1329, false)]
    public void IsTier3Mount_AcceptsExactlyTheEightTier3Ids(int itemId, bool expected)
    {
        Assert.Equal(expected, MountCatalog.IsTier3Mount(itemId));

        // Cross-check: these eight ids are also reachable through the aggregate (first fall-through step),
        // even though they are not part of the aggregate's own literal match list.
        Assert.Equal(expected, MountCatalog.IsRecognizedMount(itemId));
    }

    [Fact]
    public void IsTier3Mount_SetMatchesTheIndependentlyRecoveredMountBox635Pool()
    {
        // The contract's own corroboration: these two sets were recovered independently (this contract's tier
        // table vs. box 635's loot pool) yet cite the same ANIMAL_NUM_* constants and must agree exactly.
        foreach (var rewardItemId in MountBox635RewardTable.RewardItemIds)
            Assert.True(MountCatalog.IsTier3Mount(rewardItemId));
    }

    [Fact]
    public void IsRecognizedMount_RecognizesPuma3ViaTheSecondFallThroughStep()
    {
        // Puma3 (ANIMAL_NUM_PUMA3 = 1331) is not part of the tier-3 gate, so it is only reached by the
        // aggregate's second, final fall-through (tried after the tier-3 gate itself fails to match it).
        Assert.Equal(1331, MountCatalog.Puma3Id);
        Assert.False(MountCatalog.IsTier3Mount(MountCatalog.Puma3Id));
        Assert.True(MountCatalog.IsRecognizedMount(MountCatalog.Puma3Id));
    }

    [Fact]
    public void IsRecognizedMount_RemainingTierMemberIdsAreNotRecognizedYet()
    {
        // Documented GAP: tier-1/tier-2 ids, the remaining two Puma variants (Puma1/Puma2), and the
        // Christmas-mount id were not transcribed into the contract with a concrete, cross-verifiable numeric
        // value, so a false here does NOT prove an id is not a mount (e.g. 1301 == ANIMAL_NUM_TIGER1 is a real
        // tier-1 mount). Guards against anyone wiring this as a rejection gate before the rest of the tier
        // table is supplied.
        Assert.False(MountCatalog.IsRecognizedMount(1301));
    }
}
