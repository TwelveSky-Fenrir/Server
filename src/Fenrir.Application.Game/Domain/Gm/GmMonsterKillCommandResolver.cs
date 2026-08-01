using Fenrir.Application.Game.Abstractions.Sessions;

namespace Fenrir.Application.Game.Domain.Gm;

public enum GmMonsterKillCommandOutcome
{
    NotThisCommand,

    NotAuthorized,

    Refused
}

// Server/ts25zone/S04_MyWork04.cpp:1722-1730 : case 527 vivant dans les deux builds, corps vide hors la garde GM.
// Tuer un monstre ici serait un AJOUT : aucun chemin ne pose tResult = 0, la queue 2117-2118 repond toujours 1.
public static class GmMonsterKillCommandResolver
{
    public const int Sort = 527;

    public const string CommandName = "MON KILL";

    // uUserSort >= 1 : le plus bas des trois paliers GM de ProcessForData (S04_MyWork04.cpp:1724).
    public const GmCommandTier RequiredTier = GmCommandTier.Basic;

    // Aucun octet de tData n'est lu ni caste par le case : il n'y a aucun index client a borner ici.
    public static GmMonsterKillCommandOutcome Evaluate(int sort, bool callerMeetsRequiredTier)
    {
        if (sort != Sort)
            return GmMonsterKillCommandOutcome.NotThisCommand;

        return callerMeetsRequiredTier
            ? GmMonsterKillCommandOutcome.Refused
            : GmMonsterKillCommandOutcome.NotAuthorized;
    }
}
