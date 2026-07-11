using Fenrir.Application.Game.Domain.Mounts;

namespace Fenrir.Application.Game.Tests.Mounts;

public class MountCatalogTests
{
    [Theory]
    [InlineData(559, true)]
    [InlineData(1331, false)]
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

    [Fact]
    public void IsRecognizedMount_TierMemberIdsAreNotRecognizedYet()
    {
        // Documented GAP: tier-1..3 / Puma / Christmas member ids were not transcribed into the contract, so a
        // false here does NOT prove an id is not a mount (e.g. 1301 == ANIMAL_NUM_TIGER1 is a real tier-1
        // mount). Guards against anyone wiring this as a rejection gate before the tier table is supplied.
        Assert.False(MountCatalog.IsRecognizedMount(1301));
    }
}
