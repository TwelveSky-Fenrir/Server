using Fenrir.Application.Game.Abstractions.Sessions;

namespace Fenrir.Application.Game.Domain.Gm;

public enum Zone124DuelEndOutcome
{
    NotThisCommand,

    NotAuthorized,

    WrongMap,

    Authorized
}

// Server/ts25zone/S04_MyWork04.cpp:1948-1991 : case 602 DUEL END, sans #ifdef, vivant sous M33 et LNW33EU.
// Le corps ne caste jamais tData et ne lit aucun champ du paquet : ce sort ne recoit aucun index du pair.
public static class Zone124DuelEndResolver
{
    public const int Sort = 602;

    public const string CommandName = "DUEL-END";

    // uUserSort >= 1 : le plus bas des trois paliers GM de ProcessForData (S04_MyWork04.cpp:1949).
    public const GmCommandTier RequiredTier = GmCommandTier.Basic;

    // S04_MyWork04.cpp:1955 : verrou en dur sur le numero de zone brut, pas sur mCheckZone124TypeServer.
    public const short MapId = 124;

    // S04_MyWork04.cpp:1986 : arret administratif, tResult toujours 0. Aucun vainqueur ne peut etre calcule
    // car mDuel_124_AvatarNum est deja remis a zero en 1964-1965, avant la boucle.
    public const int Result = 0;

    // S04_MyWork04.cpp:1988 : AVATAR_CHANGE_INFO_1 tSort 7 = etat de duel, les trois valeurs sont nulles.
    public const int ClearedDuelStateSort = 7;

    // Server/Header/Protocol/DEFINE.h:139 UNIT_SCALE_RADIUS1 = 1, et DEFINE.h:598 MAX_RADIUS_FOR_NETWORK = 1000
    // vaut exactement le AoiCellSize par defaut : le rayon de Broadcast11 est donc une cellule AOI.
    public const int BroadcastScale = 1;

    public static Zone124DuelEndOutcome Evaluate(int sort, bool callerMeetsRequiredTier, short mapId)
    {
        if (sort != Sort)
            return Zone124DuelEndOutcome.NotThisCommand;

        if (!callerMeetsRequiredTier)
            return Zone124DuelEndOutcome.NotAuthorized;

        return mapId == MapId ? Zone124DuelEndOutcome.Authorized : Zone124DuelEndOutcome.WrongMap;
    }

    // S04_MyWork04.cpp:1968-1979 : mReadyState, puis IsMovingZone, puis mDuelProcessState != 4. L'etat 4 est
    // pose aussi bien par le case 601 (1935) que par le duel 1v1 (S04_MyWork02.cpp:8138/8165) : il se lit ici
    // "duel en cours", quelle qu'en soit l'origine. Le filtre mAuthInfo.AuthType est commente 1974-1976, mort.
    public static bool Clears(bool isMovingZone, bool isDuelEngaged)
    {
        return !isMovingZone && isDuelEngaged;
    }

    // Server/ts25zone/S07_MyGame03.cpp:829-835 : Broadcast11 compare le carre de la distance 3D au carre du
    // rayon, et n'exclut jamais le sujet lui-meme.
    public static bool IsWithinBroadcastRadius(float subjectX, float subjectY, float subjectZ,
        float recipientX, float recipientY, float recipientZ, float radius)
    {
        var dx = recipientX - subjectX;
        var dy = recipientY - subjectY;
        var dz = recipientZ - subjectZ;

        return dx * dx + dy * dy + dz * dz <= radius * radius;
    }
}
