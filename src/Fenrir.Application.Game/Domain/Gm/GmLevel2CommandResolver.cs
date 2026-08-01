using Fenrir.Application.Game.Abstractions.Sessions;

namespace Fenrir.Application.Game.Domain.Gm;

public enum GmLevel2CommandOutcome
{
    NotThisCommand,

    NotAuthorized,

    Refused
}

// Server/ts25zone/S04_MyWork04.cpp:1618-1703 : case 524 vivant dans les deux builds, corps entierement commente.
// Aucun chemin ne pose tResult = 0, donc la queue commune (2113-2118) repond toujours l'echec initialise en 305.
public static class GmLevel2CommandResolver
{
    public const int Sort = 524;

    public const string CommandName = "LEVEL2";

    // uUserSort >= 1 : le plus bas des trois paliers GM de ProcessForData (S04_MyWork04.cpp:1620).
    public const GmCommandTier RequiredTier = GmCommandTier.Basic;

    public static GmLevel2CommandOutcome Evaluate(int sort, bool callerMeetsRequiredTier)
    {
        if (sort != Sort)
            return GmLevel2CommandOutcome.NotThisCommand;

        return callerMeetsRequiredTier ? GmLevel2CommandOutcome.Refused : GmLevel2CommandOutcome.NotAuthorized;
    }
}
