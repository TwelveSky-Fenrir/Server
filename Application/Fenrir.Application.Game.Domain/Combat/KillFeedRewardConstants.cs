using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Combat;

public static class KillFeedRewardConstants
{

        public const int FfaWarPointPerKill = 2;

        public const int FfaBloodPointPerKill = 2;

        public const int FfaTop1ContributionPoints = 200;

    public const int FfaTop2ContributionPoints = 100;
    public const int FfaTop3ContributionPoints = 80;

        public const int NonFfaTop1ContributionPoints = 100;

    public const int NonFfaTop2ContributionPoints = 50;
    public const int NonFfaTop3ContributionPoints = 25;

        public const int Zone267ContributionPointMultiplier = 2;

        public static readonly TimeSpan FfaAntiFarmCooldown = TimeSpan.FromMinutes(3);

        public static readonly ImmutableArray<int> FfaParticipantBundleItemIdsPlaceholder = [695, 601, 602, 2249];
}
