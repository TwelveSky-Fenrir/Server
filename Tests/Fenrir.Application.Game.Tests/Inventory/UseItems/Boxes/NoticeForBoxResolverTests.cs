using Fenrir.Application.Game.Domain.Inventory.UseItems.Boxes;

namespace Fenrir.Application.Game.Tests.Inventory.UseItems.Boxes;

public class NoticeForBoxResolverTests
{
    [Fact]
    public void OrdinaryBox_NeverBroadcasts_WhileTheRewardWhitelistIsEmpty()
    {
        var decision = NoticeForBoxResolver.Decide(601, 92286, 4);

        Assert.False(decision.ShouldBroadcast);
        Assert.False(decision.WriteEliteGainAudit);
    }

    [Fact]
    public void EliteOnlyBox_WithEliteTypedReward_FlagsGainAudit_ButStillDoesNotBroadcastWithoutWhitelist()
    {
        var decision = NoticeForBoxResolver.Decide(1035, 1500, NoticeForBoxResolver.EliteItemTypeThreshold);

        Assert.True(decision.WriteEliteGainAudit);
        Assert.False(decision.ShouldBroadcast);
    }

    [Fact]
    public void EliteOnlyBox_WithNonEliteReward_DoesNotFlagGainAudit()
    {
        var decision = NoticeForBoxResolver.Decide(1036, 1500,
            NoticeForBoxResolver.EliteItemTypeThreshold - 1);

        Assert.False(decision.WriteEliteGainAudit);
        Assert.False(decision.ShouldBroadcast);
    }
}
