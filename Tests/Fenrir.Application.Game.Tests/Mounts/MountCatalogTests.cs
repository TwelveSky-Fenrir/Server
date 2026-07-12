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

        if (expected)
            Assert.True(MountCatalog.IsRecognizedMount(itemId));
    }

    [Fact]
    public void IsTier3Mount_TrueGapBetweenFamilyBlocksIsNotRecognizedByAnyCategory()
    {
        Assert.False(MountCatalog.IsRecognizedMount(1310));
        Assert.False(MountCatalog.IsRecognizedMount(1311));
        Assert.False(MountCatalog.IsRecognizedMount(1312));
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

    [Theory]
    [InlineData(1300, false)]
    [InlineData(1301, true)]
    [InlineData(1302, true)]
    [InlineData(1303, true)]
    [InlineData(1304, false)]
    [InlineData(1313, true)]
    [InlineData(1317, true)]
    [InlineData(1320, true)]
    [InlineData(1323, true)]
    [InlineData(1326, true)]
    public void IsTier1Mount_AcceptsExactlyTheEightTier1Ids(int itemId, bool expected)
    {
        Assert.Equal(expected, MountCatalog.IsTier1Mount(itemId));

        if (expected)
            Assert.True(MountCatalog.IsRecognizedMount(itemId));
    }

    [Fact]
    public void IsTier1Mount_TrueGapBeforeTheFirstFamilyBlockIsNotRecognizedByAnyCategory()
    {
        Assert.False(MountCatalog.IsRecognizedMount(1300));
    }

    [Theory]
    [InlineData(1303, false)]
    [InlineData(1304, true)]
    [InlineData(1305, true)]
    [InlineData(1306, true)]
    [InlineData(1307, false)]
    [InlineData(1314, true)]
    [InlineData(1318, true)]
    [InlineData(1321, true)]
    [InlineData(1324, true)]
    [InlineData(1327, true)]
    public void IsTier2Mount_AcceptsExactlyTheEightTier2Ids(int itemId, bool expected)
    {
        Assert.Equal(expected, MountCatalog.IsTier2Mount(itemId));

        if (expected)
            Assert.True(MountCatalog.IsRecognizedMount(itemId));
    }

    [Fact]
    public void IsRecognizedMount_RecognizesPuma1AndPuma2()
    {
        Assert.False(MountCatalog.IsTier1Mount(MountCatalog.Puma1Id));
        Assert.False(MountCatalog.IsTier2Mount(MountCatalog.Puma1Id));
        Assert.True(MountCatalog.IsRecognizedMount(MountCatalog.Puma1Id));

        Assert.False(MountCatalog.IsTier1Mount(MountCatalog.Puma2Id));
        Assert.False(MountCatalog.IsTier2Mount(MountCatalog.Puma2Id));
        Assert.True(MountCatalog.IsRecognizedMount(MountCatalog.Puma2Id));
    }

    [Fact]
    public void IsRecognizedMount_RecognizesTheStandaloneChristmasId()
    {
        Assert.Equal(1316, MountCatalog.ChristmasId);
        Assert.False(MountCatalog.IsTier1Mount(MountCatalog.ChristmasId));
        Assert.False(MountCatalog.IsTier2Mount(MountCatalog.ChristmasId));
        Assert.False(MountCatalog.IsTier3Mount(MountCatalog.ChristmasId));
        Assert.True(MountCatalog.IsRecognizedMount(MountCatalog.ChristmasId));
    }

    [Theory]
    [InlineData(0, 19011, false)]
    [InlineData(9, 19011, false)]
    [InlineData(10, 19011, true)]
    [InlineData(10, 1, false)]
    [InlineData(10, 0, false)]
    public void ResolveActiveMountItemId_GatesOnBothWornMarkerAndRecognizedCatalog(int storedSlotMarker,
        int storedMountItemId, bool expectRecognizedId)
    {
        var resolved = MountCatalog.ResolveActiveMountItemId(storedMountItemId, storedSlotMarker);

        Assert.Equal(expectRecognizedId ? storedMountItemId : MountCatalog.NoActiveMountItemId, resolved);
    }

    [Fact]
    public void ResolveActiveMountItemId_LeavesUnwornMountsUncheckedRegardlessOfCatalog()
    {
        Assert.Equal(MountCatalog.NoActiveMountItemId, MountCatalog.ResolveActiveMountItemId(19002, 9));
    }

    [Theory]
    [InlineData(19002, 15, 15)]
    [InlineData(42, 15, 5)]
    [InlineData(42, 3, 3)]
    [InlineData(0, 19, 9)]
    public void ResolveDisplayedSlotMarker_DemotesOnlyAWornButUnrecognizedMarker(int storedMountItemId,
        int storedSlotMarker, int expectedMarker)
    {
        Assert.Equal(expectedMarker, MountCatalog.ResolveDisplayedSlotMarker(storedMountItemId, storedSlotMarker));
    }
}
