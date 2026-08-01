using Fenrir.Application.Game.Abstractions.Sessions;

namespace Fenrir.Application.Game.Domain.Gm;

public enum GmLevel2CommandOutcome
{
    NotThisCommand,

    NotAuthorized,

    Refused
}

public static class GmLevel2CommandResolver
{
    public const int Sort = 524;

    public const string CommandName = "LEVEL2";

    public const GmCommandTier RequiredTier = GmCommandTier.Basic;

    public static GmLevel2CommandOutcome Evaluate(int sort, bool callerMeetsRequiredTier)
    {
        if (sort != Sort)
            return GmLevel2CommandOutcome.NotThisCommand;

        return callerMeetsRequiredTier ? GmLevel2CommandOutcome.Refused : GmLevel2CommandOutcome.NotAuthorized;
    }
}
