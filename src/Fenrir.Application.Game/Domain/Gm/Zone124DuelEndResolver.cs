using Fenrir.Application.Game.Abstractions.Sessions;

namespace Fenrir.Application.Game.Domain.Gm;

public enum Zone124DuelEndOutcome
{
    NotThisCommand,

    NotAuthorized,

    WrongMap,

    Authorized
}

public static class Zone124DuelEndResolver
{
    public const int Sort = 602;

    public const string CommandName = "DUEL-END";

    public const GmCommandTier RequiredTier = GmCommandTier.Basic;

    public const short MapId = 124;

    public const int Result = 0;

    public const int ClearedDuelStateSort = 7;

    public const int BroadcastScale = 1;

    public static Zone124DuelEndOutcome Evaluate(int sort, bool callerMeetsRequiredTier, short mapId)
    {
        if (sort != Sort)
            return Zone124DuelEndOutcome.NotThisCommand;

        if (!callerMeetsRequiredTier)
            return Zone124DuelEndOutcome.NotAuthorized;

        return mapId == MapId ? Zone124DuelEndOutcome.Authorized : Zone124DuelEndOutcome.WrongMap;
    }

    public static bool Clears(bool isMovingZone, bool isDuelEngaged)
    {
        return !isMovingZone && isDuelEngaged;
    }

    public static bool IsWithinBroadcastRadius(float subjectX, float subjectY, float subjectZ,
        float recipientX, float recipientY, float recipientZ, float radius)
    {
        var dx = recipientX - subjectX;
        var dy = recipientY - subjectY;
        var dz = recipientZ - subjectZ;

        return dx * dx + dy * dy + dz * dz <= radius * radius;
    }
}
