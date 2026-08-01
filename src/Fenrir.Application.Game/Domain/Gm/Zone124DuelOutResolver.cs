using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Domain.Gm;

public enum Zone124DuelOutOutcome
{
    NotThisCommand,

    NotAuthorized,

    WrongMap,

    Authorized
}

// Server/ts25zone/S04_MyWork04.cpp:1992-2057 : case 603 DUEL OUT, sans #ifdef, vivant sous M33 et LNW33EU.
// Le tData du client n'est jamais decode (ligne 2003 le reexpedie en bloc) : ce sort ne recoit aucun index.
public static class Zone124DuelOutResolver
{
    public const int Sort = 603;

    public const string CommandName = "DUEL-OUT";

    // uUserSort >= 1 : le plus bas des trois paliers GM de ProcessForData (S04_MyWork04.cpp:1993).
    public const GmCommandTier RequiredTier = GmCommandTier.Basic;

    // Server/ts25zone/S07_MyGame01.cpp:509-516 : 124 est la seule map marquee mCheckZone124TypeServer.
    public const short MapId = Zone124DuelOverrideResolver.Zone124MapId;

    public const float PadRadius = 52f;

    // Server/ts25zone/S04_MyWork04.cpp:2041-2043 : point de repli, distinct de tout pad de duel.
    public static readonly (float X, float Y, float Z) EvacuationPoint = (0f, 36f, -209f);

    // Server/ts25zone/S04_MyWork04.cpp:2011-2016 : les deux pads de depart poses par le case 601 (1891-1896).
    public static readonly (float X, float Y, float Z) WestPad = (-157f, 5f, 1f);

    public static readonly (float X, float Y, float Z) EastPad = (157f, 5f, 1f);

    private const float ArenaBoundX = 122f;

    private const float ArenaBoundZ = 124f;

    public static Zone124DuelOutOutcome Evaluate(int sort, bool callerMeetsRequiredTier, short mapId)
    {
        if (sort != Sort)
            return Zone124DuelOutOutcome.NotThisCommand;

        if (!callerMeetsRequiredTier)
            return Zone124DuelOutOutcome.NotAuthorized;

        return MassDuelArenaCatalog.IsMassDuelArena(mapId)
            ? Zone124DuelOutOutcome.Authorized
            : Zone124DuelOutOutcome.WrongMap;
    }

    // Server/ts25zone/S04_MyWork04.cpp:2019-2038 : saut IsMovingZone puis un OU de trois tests geometriques.
    // Le filtre mAuthInfo.AuthType == 1 est commente lignes 2026-2028, donc mort : il n'est pas porte.
    public static bool IsInsideArena(bool isMovingZone, float x, float y, float z)
    {
        if (isMovingZone)
            return false;

        return IsInsideArenaBox(x, z) || IsInsidePad(WestPad, x, y, z) || IsInsidePad(EastPad, x, y, z);
    }

    // Server/ts25zone/S04_MyWork04.cpp:2029-2030 : bornes STRICTES, et l'axe Y n'est deliberement pas teste ici.
    private static bool IsInsideArenaBox(float x, float z)
    {
        return x > -ArenaBoundX && x < ArenaBoundX && z > -ArenaBoundZ && z < ArenaBoundZ;
    }

    // Server/Header/mapcheck.h:12-15 : GetLengthXYZ est un sqrtf 3D complet, Y inclus. Le rayon 52 est une sphere.
    private static bool IsInsidePad((float X, float Y, float Z) pad, float x, float y, float z)
    {
        var dx = pad.X - x;
        var dy = pad.Y - y;
        var dz = pad.Z - z;

        return MathF.Sqrt(dx * dx + dy * dy + dz * dz) < PadRadius;
    }
}
