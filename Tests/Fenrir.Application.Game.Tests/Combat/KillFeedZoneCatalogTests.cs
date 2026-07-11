using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Tests.Combat;

public class KillFeedZoneCatalogTests
{
    [Fact]
    public void HasLeaderboardStore_Zone049TypeMap_True()
    {
        // 49 is the representative Zone049-type map; RegularWarMapCatalog also lists 146 as another member
        // of the same family.
        Assert.True(KillFeedZoneCatalog.HasLeaderboardStore(49));
        Assert.True(KillFeedZoneCatalog.HasLeaderboardStore(146));
    }

    [Fact]
    public void HasLeaderboardStore_FfaMap_True()
    {
        Assert.True(KillFeedZoneCatalog.HasLeaderboardStore(KillFeedZoneCatalog.FfaMapNumber));
    }

    [Fact]
    public void HasLeaderboardStore_Zone267_True()
    {
        Assert.True(KillFeedZoneCatalog.HasLeaderboardStore(KillFeedZoneCatalog.Zone267MapNumber));
    }

    [Fact]
    public void HasLeaderboardStore_UnlistedZone_False()
    {
        Assert.False(KillFeedZoneCatalog.HasLeaderboardStore(1));
    }

    [Fact]
    public void IsFeedEnabled_MatchesHasLeaderboardStore_ForEveryModeledZoneType()
    {
        Assert.True(KillFeedZoneCatalog.IsFeedEnabled(49));
        Assert.True(KillFeedZoneCatalog.IsFeedEnabled(KillFeedZoneCatalog.FfaMapNumber));
        Assert.True(KillFeedZoneCatalog.IsFeedEnabled(KillFeedZoneCatalog.Zone267MapNumber));
        Assert.False(KillFeedZoneCatalog.IsFeedEnabled(1));
    }

    [Fact]
    public void FfaMapNumber_MatchesSharedConstantElsewhere()
    {
        Assert.Equal(PvpKillRewardZoneCatalog.FfaMapNumber, KillFeedZoneCatalog.FfaMapNumber);
    }
}
