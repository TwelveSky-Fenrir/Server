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

public static class Zone124DuelStartResolver
{
    public const int Sort = 601;

    public const string CommandName = "DUEL-START";

    public const GmCommandTier RequiredTier = GmCommandTier.Basic;

    public const short MapId = 124;

    public const float RecruitRadius = 16f;

    public const int DurationUnits = 60;

    public const int EatDrugState = 1;

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

    public static int[] BuildDuelState(Zone124DuelStartSide side, int sessionNumber)
    {
        return [DuelStateEngaged, sessionNumber, (int)side];
    }

    public static int AllocateSessionNumber(int callerCharacterId, long zoneLogicTick)
    {
        var sessionNumber = HashCode.Combine(callerCharacterId, zoneLogicTick) & int.MaxValue;
        return sessionNumber == 0 ? 1 : sessionNumber;
    }

    private static bool IsInsideLineUp((float X, float Y, float Z) lineUp, float x, float y, float z)
    {
        var dx = lineUp.X - x;
        var dy = lineUp.Y - y;
        var dz = lineUp.Z - z;

        return MathF.Sqrt(dx * dx + dy * dy + dz * dz) < RecruitRadius;
    }
}
