namespace Fenrir.Application.Game.Domain.Combat;

public static class KillFeedRewardConstants
{
    public const int FfaWarPointPerKill = 2;

    public const int FfaBloodPointPerKill = 2;

    public const int Top1ContributionPoints = 100;

    public const int Top2ContributionPoints = 50;

    public const int Top3ContributionPoints = 25;

    public const int Zone267ContributionPointMultiplier = 2;

    public static readonly TimeSpan FfaAntiFarmCooldown = TimeSpan.FromMinutes(3);
}
