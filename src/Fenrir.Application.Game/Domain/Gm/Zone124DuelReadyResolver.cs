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

public static class Zone124DuelReadyResolver
{
    public const int Sort = 600;

    public const string CommandName = "DUEL-READY";

    public const GmCommandTier RequiredTier = GmCommandTier.Basic;

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

    private static bool IsInsideMuster((float X, float Y, float Z) muster, float x, float y, float z)
    {
        var dx = muster.X - x;
        var dy = muster.Y - y;
        var dz = muster.Z - z;

        return MathF.Sqrt(dx * dx + dy * dy + dz * dz) < MusterRadius;
    }
}
