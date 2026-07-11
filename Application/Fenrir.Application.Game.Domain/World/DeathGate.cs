namespace Fenrir.Application.Game.Domain.World;

public enum ReviveZoneKind
{

        FactionTerritory,

        AlwaysBlocked,

        Unconditional
}

public static class ReviveEligibilityZones
{

        public const short AlwaysBlockedZoneId = 200;

        public const short UnconditionalZoneIdA = 322;

        public const short UnconditionalZoneIdB = 323;

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
            _ => (ReviveZoneKind.Unconditional, default)
        };
    }
}

public static class ReviveEligibilityRules
{

        public const int DeathSubCounterBaseline = 0;

        public static bool IsEligible(short mapId, byte avatarTribe, byte? avatarAlliedTribe)
    {
        var (kind, owningFaction) = ReviveEligibilityZones.Classify(mapId);

        return kind switch
        {
            ReviveZoneKind.AlwaysBlocked => false,
            ReviveZoneKind.Unconditional => true,
            ReviveZoneKind.FactionTerritory => avatarTribe == owningFaction || avatarAlliedTribe == owningFaction,
            _ => true
        };
    }
}

public static class ZoneTransferAntiAbuseRules
{

        public const short ExemptDestinationZoneId = 38;

        public static bool AllowsTransferWhileFlagged(short currentMapId, short destinationMapId, byte avatarTribe,
        Func<byte, byte?> currentZoneOwningFactionAlly)
    {
        if (destinationMapId == ExemptDestinationZoneId)
            return true;

        var (kind, owningFaction) = ReviveEligibilityZones.Classify(currentMapId);
        if (kind != ReviveZoneKind.FactionTerritory)
            return true;

        if (avatarTribe == owningFaction)
            return true;

        return currentZoneOwningFactionAlly(owningFaction) == 0;
    }
}
