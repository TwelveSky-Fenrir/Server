using Fenrir.Application.Game.Abstractions.Sessions;

namespace Fenrir.Application.Game.Domain.Gm;

public enum GmDeleteItemCommandOutcome
{
    NotThisCommand,

    NotAuthorized,

    Refused
}

public static class GmDeleteItemCommandResolver
{
    public const int Sort = 526;

    public const string CommandName = "DEL ITEM";

    public const GmCommandTier RequiredTier = GmCommandTier.Basic;

    public static GmDeleteItemCommandOutcome Evaluate(int sort, bool callerMeetsRequiredTier)
    {
        if (sort != Sort)
            return GmDeleteItemCommandOutcome.NotThisCommand;

        return callerMeetsRequiredTier ? GmDeleteItemCommandOutcome.Refused : GmDeleteItemCommandOutcome.NotAuthorized;
    }
}
