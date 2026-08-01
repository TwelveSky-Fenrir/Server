using Fenrir.Application.Game.Abstractions.Sessions;

namespace Fenrir.Application.Game.Domain.Gm;

public enum Zone124DuelReadyOutcome
{
    NotThisCommand,

    NotAuthorized,

    WrongMap,

    Authorized
}

public enum Zone124DuelReadySide : byte
{
    None,

    West,

    East
}

public readonly record struct Zone124DuelReadyPlacement(Zone124DuelReadySide Side, float X, float Y, float Z);

// Server/ts25zone/S04_MyWork04.cpp:1821-1874 : case 600 DUEL READY, sans #ifdef, vivant sous M33 et LNW33EU.
// Le tData du client n'est jamais decode (ligne 1832 le reexpedie en bloc) : ce sort ne recoit aucun index.
public static class Zone124DuelReadyResolver
{
    public const int Sort = 600;

    public const string CommandName = "DUEL-READY";

    // uUserSort >= 1 : le plus bas des trois paliers GM de ProcessForData (S04_MyWork04.cpp:1822).
    public const GmCommandTier RequiredTier = GmCommandTier.Basic;

    // Server/ts25zone/S07_MyGame01.cpp:509-516 : 124 est la seule map marquee mCheckZone124TypeServer.
    public const short MapId = 124;

    public const float MusterRadius = 52f;

    public static readonly (float X, float Y, float Z) WestMuster = (-232f, 36f, 2f);

    public static readonly (float X, float Y, float Z) EastMuster = (232f, 36f, 2f);

    public static readonly (float X, float Y, float Z) WestLineUp = (-157f, 5f, 1f);

    public static readonly (float X, float Y, float Z) EastLineUp = (157f, 5f, 1f);

    public static Zone124DuelReadyOutcome Evaluate(int sort, bool callerMeetsRequiredTier, short mapId)
    {
        if (sort != Sort)
            return Zone124DuelReadyOutcome.NotThisCommand;

        if (!callerMeetsRequiredTier)
            return Zone124DuelReadyOutcome.NotAuthorized;

        return mapId == MapId ? Zone124DuelReadyOutcome.Authorized : Zone124DuelReadyOutcome.WrongMap;
    }

    // Server/ts25zone/S04_MyWork04.cpp:1843-1872 : sauts IsMovingZone / mDuelProcessState, puis les deux spheres.
    // Le filtre mAuthInfo.AuthType == 1 est commente lignes 1849-1851, donc mort : il n'est pas porte.
    public static Zone124DuelReadyPlacement Place(bool isMovingZone, bool isDuelEngaged, float x, float y, float z)
    {
        if (isMovingZone || isDuelEngaged)
            return default;

        if (IsInsideMuster(WestMuster, x, y, z))
            return new Zone124DuelReadyPlacement(Zone124DuelReadySide.West, WestLineUp.X, WestLineUp.Y,
                WestLineUp.Z);

        return IsInsideMuster(EastMuster, x, y, z)
            ? new Zone124DuelReadyPlacement(Zone124DuelReadySide.East, EastLineUp.X, EastLineUp.Y, EastLineUp.Z)
            : default;
    }

    // Server/ts25zone/S07_MyGame03.cpp:4704-4707 : ReturnLengthXYZ est un sqrtf 3D complet, Y inclus.
    // Le rayon 52 est donc une sphere et non un cylindre : ne pas laisser tomber le terme Y.
    private static bool IsInsideMuster((float X, float Y, float Z) muster, float x, float y, float z)
    {
        var dx = muster.X - x;
        var dy = muster.Y - y;
        var dz = muster.Z - z;

        return MathF.Sqrt(dx * dx + dy * dy + dz * dz) < MusterRadius;
    }
}
