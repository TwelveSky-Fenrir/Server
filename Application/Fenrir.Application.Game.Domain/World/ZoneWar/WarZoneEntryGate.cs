namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public enum WarZoneEntryOutcome
{

        Allowed,

        RejectedOutOfRange
}

public static class WarZoneEntryGate
{

        public static WarZoneEntryOutcome Evaluate(short zoneNumber, int combinedLevel, int rebirthCount)
    {
        if (!WarZoneEntryCatalog.TryGetRule(zoneNumber, out var rule))
            return WarZoneEntryOutcome.Allowed;

        var levelOk = combinedLevel >= rule.MinCombinedLevel && combinedLevel <= rule.MaxCombinedLevel;
        var rebirthOk = rebirthCount >= rule.MinRebirthCount && rebirthCount <= rule.MaxRebirthCount;

        return levelOk && rebirthOk ? WarZoneEntryOutcome.Allowed : WarZoneEntryOutcome.RejectedOutOfRange;
    }
}
