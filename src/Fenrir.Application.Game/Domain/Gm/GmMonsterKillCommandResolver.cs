using Fenrir.Application.Game.Abstractions.Sessions;

namespace Fenrir.Application.Game.Domain.Gm;

public enum GmMonsterKillCommandOutcome
{
    NotThisCommand,

    NotAuthorized,

    Refused
}

public static class GmMonsterKillCommandResolver
{
    public const int Sort = 527;

    public const string CommandName = "MON KILL";

    public const GmCommandTier RequiredTier = GmCommandTier.Basic;

    public static GmMonsterKillCommandOutcome Evaluate(int sort, bool callerMeetsRequiredTier)
    {
        if (sort != Sort)
            return GmMonsterKillCommandOutcome.NotThisCommand;

        return callerMeetsRequiredTier
            ? GmMonsterKillCommandOutcome.Refused
            : GmMonsterKillCommandOutcome.NotAuthorized;
    }
}
