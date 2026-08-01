using Fenrir.Application.Game.Abstractions.Sessions;

namespace Fenrir.Application.Game.Domain.Gm;

public enum GmStubCommandOutcome
{
    NotAStubCommand,

    NotAuthorized,

    NoOpFailure
}

public static class GmStubCommandResolver
{
    public const int UseItemSort = 525;

    public const GmCommandTier RequiredTier = GmCommandTier.Basic;

    private const int GameMasterMinimumUserSort = (int)RequiredTier;

    public static bool IsStubCommand(int sort)
    {
        return sort is UseItemSort;
    }

    public static GmStubCommandOutcome Evaluate(int sort, int callerUserSort)
    {
        if (!IsStubCommand(sort))
            return GmStubCommandOutcome.NotAStubCommand;

        return callerUserSort < GameMasterMinimumUserSort
            ? GmStubCommandOutcome.NotAuthorized
            : GmStubCommandOutcome.NoOpFailure;
    }
}
