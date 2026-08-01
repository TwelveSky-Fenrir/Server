using Fenrir.Application.Game.Abstractions.Sessions;

namespace Fenrir.Application.Game.Domain.Gm;

public enum GmDeleteItemCommandOutcome
{
    NotThisCommand,

    NotAuthorized,

    Refused
}

// Server/ts25zone/S04_MyWork04.cpp:1713-1721 : case 526 vivant dans les deux builds, corps vide hors la garde GM.
// Aucun chemin ne pose tResult = 0, donc la queue commune (2113-2118) repond toujours l'echec initialise en 305.
public static class GmDeleteItemCommandResolver
{
    public const int Sort = 526;

    public const string CommandName = "DEL ITEM";

    // uUserSort >= 1 : le plus bas des trois paliers GM de ProcessForData (S04_MyWork04.cpp:1715).
    public const GmCommandTier RequiredTier = GmCommandTier.Basic;

    // Aucun octet de tData n'est lu ni dereference par ce case : il n'y a aucun index a borner.
    public static GmDeleteItemCommandOutcome Evaluate(int sort, bool callerMeetsRequiredTier)
    {
        if (sort != Sort)
            return GmDeleteItemCommandOutcome.NotThisCommand;

        return callerMeetsRequiredTier ? GmDeleteItemCommandOutcome.Refused : GmDeleteItemCommandOutcome.NotAuthorized;
    }
}
