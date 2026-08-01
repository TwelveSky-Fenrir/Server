using Fenrir.Application.Game.Abstractions.Sessions;

namespace Fenrir.Application.Game.Domain.Gm;

public enum Zone124DuelStartOutcome
{
    NotThisCommand,

    NotAuthorized,

    WrongMap,

    Authorized
}

public enum Zone124DuelStartSide : byte
{
    None,

    West,

    East
}

public readonly record struct Zone124DuelStartRecruitment(Zone124DuelStartSide Side, float X, float Y, float Z);

// Server/ts25zone/S04_MyWork04.cpp:1875-1947 : case 601 DUEL START, sans #ifdef, vivant sous M33 et LNW33EU.
// Le tData du client n'est jamais decode (ligne 1886 le reexpedie en bloc) : ce sort ne recoit aucun index.
public static class Zone124DuelStartResolver
{
    public const int Sort = 601;

    public const string CommandName = "DUEL-START";

    // uUserSort >= 1 : le plus bas des trois paliers GM de ProcessForData (S04_MyWork04.cpp:1876).
    public const GmCommandTier RequiredTier = GmCommandTier.Basic;

    // Server/ts25zone/S07_MyGame01.cpp:509-516 : 124 est la seule map marquee mCheckZone124TypeServer.
    public const short MapId = 124;

    public const float RecruitRadius = 16f;

    // Server/ts25zone/S04_MyWork04.cpp:1900 : mDuel_124_RemainTime, decremente un tick sur deux (S07_MyGame01.cpp:4053).
    public const int DurationUnits = 60;

    // Server/ts25zone/S04_MyWork04.cpp:1937 : tEatDrugState en dur, les potions restent autorisees.
    public const int EatDrugState = 1;

    // Server/ts25zone/S07_MyGame01.cpp:4011-4032 : un camp vide fait diffuser DUEL_END a toute la zone au tick
    // suivant. Le legacy pose mDuel_124_Pvp inconditionnellement (1946) ; Fenrir refuse l'ordre a la place.
    public const int MinimumRecruitsPerCamp = 1;

    private const int DuelStateEngaged = 1;

    public static readonly (float X, float Y, float Z) WestLineUp = (-157f, 5f, 1f);

    public static readonly (float X, float Y, float Z) EastLineUp = (157f, 5f, 1f);

    public static readonly (float X, float Y, float Z) WestStartPoint = (-100f, 2f, 0f);

    public static readonly (float X, float Y, float Z) EastStartPoint = (100f, 2f, 0f);

    public static Zone124DuelStartOutcome Evaluate(int sort, bool callerMeetsRequiredTier, short mapId)
    {
        if (sort != Sort)
            return Zone124DuelStartOutcome.NotThisCommand;

        if (!callerMeetsRequiredTier)
            return Zone124DuelStartOutcome.NotAuthorized;

        return mapId == MapId ? Zone124DuelStartOutcome.Authorized : Zone124DuelStartOutcome.WrongMap;
    }

    // Server/ts25zone/S04_MyWork04.cpp:1903-1930 : sauts IsMovingZone / mDuelProcessState, puis les deux spheres.
    // Le filtre mAuthInfo.AuthType == 1 est commente lignes 1909-1911, donc mort : il n'est pas porte.
    public static Zone124DuelStartRecruitment Recruit(bool isMovingZone, bool isDuelEngaged, float x, float y,
        float z)
    {
        if (isMovingZone || isDuelEngaged)
            return default;

        if (IsInsideLineUp(WestLineUp, x, y, z))
            return new Zone124DuelStartRecruitment(Zone124DuelStartSide.West, WestStartPoint.X, WestStartPoint.Y,
                WestStartPoint.Z);

        return IsInsideLineUp(EastLineUp, x, y, z)
            ? new Zone124DuelStartRecruitment(Zone124DuelStartSide.East, EastStartPoint.X, EastStartPoint.Y,
                EastStartPoint.Z)
            : default;
    }

    public static bool HasBothCamps(int westCount, int eastCount)
    {
        return westCount >= MinimumRecruitsPerCamp && eastCount >= MinimumRecruitsPerCamp;
    }

    // Server/ts25zone/S07_MyGame02.cpp:670-679 : le gate PvP compare aDuelState membre a membre, [1] identique et
    // [2] different. Le camp voyage donc dans le troisieme entier, aux valeurs 1 et 2 du legacy.
    public static int[] BuildDuelState(Zone124DuelStartSide side, int sessionNumber)
    {
        return [DuelStateEngaged, sessionNumber, (int)side];
    }

    // Server/ts25zone/S07_MyGame01.cpp:462 : mAvatarDuelUniqueNumber est un compteur de zone monotone jamais
    // remis a zero. Ici seule l'egalite entre recrues d'un meme appel porte du sens, jamais l'ordre.
    public static int AllocateSessionNumber(int callerCharacterId, long zoneLogicTick)
    {
        var sessionNumber = HashCode.Combine(callerCharacterId, zoneLogicTick) & int.MaxValue;
        return sessionNumber == 0 ? 1 : sessionNumber;
    }

    // Server/Header/mapcheck.h:12-15 : GetLengthXYZ est un sqrtf 3D complet, Y inclus. Le rayon 16 est une sphere.
    private static bool IsInsideLineUp((float X, float Y, float Z) lineUp, float x, float y, float z)
    {
        var dx = lineUp.X - x;
        var dy = lineUp.Y - y;
        var dz = lineUp.Z - z;

        return MathF.Sqrt(dx * dx + dy * dy + dz * dz) < RecruitRadius;
    }
}
