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

public static class Zone124DuelOutResolver
{
    public const int Sort = 603;

    public const string CommandName = "DUEL-OUT";

    public const GmCommandTier RequiredTier = GmCommandTier.Basic;

    public const short MapId = Zone124DuelOverrideResolver.Zone124MapId;

    public const float PadRadius = 52f;

    private const float ArenaBoundX = 122f;

    private const float ArenaBoundZ = 124f;

    public static readonly (float X, float Y, float Z) EvacuationPoint = (0f, 36f, -209f);

    public static readonly (float X, float Y, float Z) WestPad = (-157f, 5f, 1f);

    public static readonly (float X, float Y, float Z) EastPad = (157f, 5f, 1f);

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

    public static bool IsInsideArena(bool isMovingZone, float x, float y, float z)
    {
        if (isMovingZone)
            return false;

        return IsInsideArenaBox(x, z) || IsInsidePad(WestPad, x, y, z) || IsInsidePad(EastPad, x, y, z);
    }

    private static bool IsInsideArenaBox(float x, float z)
    {
        return x > -ArenaBoundX && x < ArenaBoundX && z > -ArenaBoundZ && z < ArenaBoundZ;
    }

    private static bool IsInsidePad((float X, float Y, float Z) pad, float x, float y, float z)
    {
        var dx = pad.X - x;
        var dy = pad.Y - y;
        var dz = pad.Z - z;

        return MathF.Sqrt(dx * dx + dy * dy + dz * dz) < PadRadius;
    }
}
