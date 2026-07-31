using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Data.WriteBehind;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class HolyStoneCaptureRewardGateway(
    HeroRankPointAccumulator heroRankPoints,
    DirtyTracker<int> dirtyTracker,
    ILogger<HolyStoneCaptureRewardGateway> logger) : IHolyStoneCaptureRewardGateway
{
    public const int CapturerContributionPoints = 50;

    public const int CapturerHeroRankPoints = 50;

    public const int ParticipationContributionPoints = 10;

    public const int ParticipationHeroRankPoints = 10;

    public const int HeroRankPointMinimumCombinedLevel = 113;

    private const int ContributionPointStatSort = 3;

    private const int HeroRankPointStatSort = 904;

    public void GrantCaptureReward(PlayerRuntimeState capturer)
    {
        GrantContributionPoints(capturer, CapturerContributionPoints);
        GrantHeroRankPoints(capturer, CapturerHeroRankPoints);

        logger.LogInformation(
            "HolyStoneWar: character {CharacterId} captured the Stone -- granted {ContributionPoints} CP",
            capturer.CharacterId, CapturerContributionPoints);
    }

    public void GrantParticipationReward(PlayerRuntimeState tribemate)
    {
        GrantContributionPoints(tribemate, ParticipationContributionPoints);
        GrantHeroRankPoints(tribemate, ParticipationHeroRankPoints);
    }

    private void GrantContributionPoints(PlayerRuntimeState player, int amount)
    {
        player.ContributionPoints = Math.Max(0, player.ContributionPoints + amount);
        player.MarkProgressDirty(dirtyTracker, DirtyFlags.Progression);
        player.Session.Send(new AvatarStatUpdateResponse
        {
            Sort = ContributionPointStatSort, Value = player.ContributionPoints, Value2 = 0
        });
    }

    private void GrantHeroRankPoints(PlayerRuntimeState player, int amount)
    {
        if (player.CombinedLevel < HeroRankPointMinimumCombinedLevel)
            return;

        player.HeroRankPoints += amount;
        heroRankPoints.AddPending(player.CharacterId, amount, player.Tribe, player.Level);
        player.Session.Send(new AvatarStatUpdateResponse
        {
            Sort = HeroRankPointStatSort, Value = player.HeroRankPoints, Value2 = 0
        });
    }
}
