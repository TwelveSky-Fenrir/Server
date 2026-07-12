namespace Fenrir.Application.Game.Domain.World;

public enum ReviveZoneKind
{
    FactionTerritory,

    AlwaysBlocked,

    DeathClearOnly,

    Unconditional
}

public static class ReviveEligibilityZones
{
    public const short AlwaysBlockedZoneId = 200;

    public const short DeathClearOnlyZoneIdA = 322;

    public const short DeathClearOnlyZoneIdB = 323;

    public const short BroadcastSuppressionExemptZoneId = 124;

    public static (ReviveZoneKind Kind, byte OwningFaction) Classify(short mapId)
    {
        return mapId switch
        {
            >= 1 and <= 4 => (ReviveZoneKind.FactionTerritory, (byte)0),
            >= 6 and <= 9 => (ReviveZoneKind.FactionTerritory, (byte)1),
            >= 11 and <= 14 => (ReviveZoneKind.FactionTerritory, (byte)2),
            >= 140 and <= 143 => (ReviveZoneKind.FactionTerritory, (byte)3),
            AlwaysBlockedZoneId => (ReviveZoneKind.AlwaysBlocked, default),
            DeathClearOnlyZoneIdA or DeathClearOnlyZoneIdB => (ReviveZoneKind.DeathClearOnly, default),
            _ => (ReviveZoneKind.Unconditional, default)
        };
    }
}

public enum ReviveClearOutcome
{
    NoChange,

    ClearDeathWindowOnly,

    ClearDeathWindowAndLock
}

public static class ReviveEligibilityRules
{
    public const int DeathSubCounterBaseline = 0;

    public static ReviveClearOutcome Resolve(short mapId, byte avatarTribe, byte? avatarAlliedTribe)
    {
        var (kind, owningFaction) = ReviveEligibilityZones.Classify(mapId);

        return kind switch
        {
            ReviveZoneKind.AlwaysBlocked => ReviveClearOutcome.NoChange,
            ReviveZoneKind.DeathClearOnly => ReviveClearOutcome.ClearDeathWindowOnly,
            ReviveZoneKind.Unconditional => ReviveClearOutcome.ClearDeathWindowAndLock,
            ReviveZoneKind.FactionTerritory => avatarTribe == owningFaction || avatarAlliedTribe == owningFaction
                ? ReviveClearOutcome.ClearDeathWindowAndLock
                : ReviveClearOutcome.NoChange,
            _ => ReviveClearOutcome.NoChange
        };
    }
}

public static class ZoneTransferAntiAbuseRules
{
    public const short ExemptDestinationZoneId = 38;

    public const byte CapitalGroupAllianceComparandTribe = 0;

    public static bool AllowsTransferWhileFlagged(short destinationMapId, Func<byte, byte?> allyOfOwningFaction)
    {
        if (destinationMapId == ExemptDestinationZoneId)
            return true;

        var (kind, owningFaction) = ReviveEligibilityZones.Classify(destinationMapId);

        return kind != ReviveZoneKind.FactionTerritory ||
               allyOfOwningFaction(owningFaction) == CapitalGroupAllianceComparandTribe;
    }
}
