using Fenrir.Application.Game.Abstractions.Sessions;

namespace Fenrir.Application.Game.Domain.Gm;

public enum GmStubCommandOutcome
{
    NotAStubCommand,

    NotAuthorized,

    NoOpFailure
}

// Server/ts25zone/S04_MyWork04.cpp:1704-1712 : corps du case 525 vide apres la garde GM, aucun effet d'etat.
// tResult reste a son initialisation 1 (S04_MyWork04.cpp:305) : il n'existe aucun chemin de succes.
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
