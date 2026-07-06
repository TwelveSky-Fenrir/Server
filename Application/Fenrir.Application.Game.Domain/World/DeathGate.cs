namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     Which of the revive-eligibility zone-identity groups a map belongs to (<c>S07_MyGame04.cpp:257-327</c>):
///     one of four faction-territory blocks, the one always-blocked identity, or everything else
///     (unconditional -- including the two identities the legacy special-cases, 322/323, which behave
///     identically to the "everything else" default).
/// </summary>
public enum ReviveZoneKind
{
    /// <summary>One of the four faction-territory blocks -- eligibility requires a faction/alliance match.</summary>
    FactionTerritory,

    /// <summary>Zone 200 -- the territorial recheck never grants eligibility here, on any tick.</summary>
    AlwaysBlocked,

    /// <summary>Every other zone identity (including 322/323) -- eligibility is unconditional past the tick threshold.</summary>
    Unconditional
}

/// <summary>
///     Zone-identity classification feeding both the tick-loop territorial revive-eligibility recheck
///     (<see cref="ReviveEligibilityRules" />) and its zone-transfer companion check
///     (<see cref="ZoneTransferAntiAbuseRules" />).
/// </summary>
public static class ReviveEligibilityZones
{
    /// <summary>The identity whose territorial recheck never grants eligibility (S07_MyGame04.cpp:257-327).</summary>
    public const short AlwaysBlockedZoneId = 200;

    /// <summary>
    ///     Explicitly named even though it folds into <see cref="ReviveZoneKind.Unconditional" /> -- same
    ///     observable behavior as "everything else," called out separately only because the legacy code has
    ///     its own explicit case for it (S07_MyGame04.cpp:257-327).
    /// </summary>
    public const short UnconditionalZoneIdA = 322;

    /// <summary>See <see cref="UnconditionalZoneIdA" />.</summary>
    public const short UnconditionalZoneIdB = 323;

    /// <summary>
    ///     The one zone identity whose own broadcast-suppression filter (side effect 3) is disabled entirely --
    ///     S07_MyGame01.cpp:508-522, gated by the unconditionally-defined <c>ZONE124</c> macro
    ///     (H07_MyGame.h:19).
    /// </summary>
    public const short BroadcastSuppressionExemptZoneId = 124;

    /// <summary>
    ///     Resolves a map id to its revive-eligibility group and, for <see cref="ReviveZoneKind.FactionTerritory" />,
    ///     the faction that owns that territory block. The owning faction is meaningless for the other two kinds.
    /// </summary>
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

/// <summary>
///     The territorial revive-eligibility recheck itself (side effect 1, <c>S07_MyGame04.cpp:257-327</c>):
///     re-evaluated every tick from the 10-tick mark onward (never a one-shot check) using the then-current
///     faction/alliance state -- a faction alliance forming or dissolving after death can flip the outcome on
///     a later tick without the avatar having done anything.
/// </summary>
public static class ReviveEligibilityRules
{
    /// <summary>
    ///     No consumer of this legacy sub-counter was located in the cited files (see the behavior contract's
    ///     own note) -- 0 is a neutral "unset" default, not a reproduction of a specific legacy numeral.
    /// </summary>
    public const int DeathSubCounterBaseline = 0;

    /// <summary>
    ///     True when territorial revive-eligibility is granted for <paramref name="avatarTribe" /> dying on
    ///     <paramref name="mapId" />, given <paramref name="avatarAlliedTribe" /> (the avatar's own currently
    ///     allied faction, or null if unallied -- <see cref="WorldState.WorldStateService.GetAllyOf" />).
    /// </summary>
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

/// <summary>
///     The zone-transfer companion check (<c>CZ_DEMAND_ZONE_SERVER_INFO_2</c> while flagged,
///     <c>S04_MyWork02.cpp:2017-2064</c>): deliberately NOT the same wording as
///     <see cref="ReviveEligibilityRules.IsEligible" /> -- see this type's own remarks for the legacy quirk
///     this preserves.
/// </summary>
/// <remarks>
///     The legacy's own alliance clause here asks "does the current zone's owning faction currently have some
///     ally at all," not "is the avatar's own faction that owning faction or allied with it" (the tick-loop
///     gate's wording). Because the alliance lookup's "no alliance" sentinel is only distinguishable from a
///     real tribe id in the faction-0 case, this collapses in practice to: for the faction-0 territory block,
///     no alliance-based leniency is ever granted (only an exact faction-0 match on the avatar avoids the
///     kick); for the other three blocks, if that block's owning faction happens to currently be allied with
///     faction 0 specifically, the kick is suspended for every avatar leaving that zone, not merely members of
///     the allied faction. Preserved verbatim rather than normalized to the cleaner tick-loop wording, since
///     the two do not currently agree.
/// </remarks>
public static class ZoneTransferAntiAbuseRules
{
    /// <summary>Always permitted regardless of the anti-abuse flag or faction alignment.</summary>
    public const short ExemptDestinationZoneId = 38;

    /// <summary>
    ///     True when a zone-transfer request from a flagged (<see cref="PlayerRuntimeState.ReviveHackFlag" />)
    ///     session must be let through rather than kicked. <paramref name="currentZoneOwningFactionAlly" />
    ///     resolves the CURRENT zone's owning faction's own ally (not the avatar's), matching this type's own
    ///     remarks on the legacy quirk.
    /// </summary>
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

        // The legacy quirk: only an alliance with faction 0 specifically suspends the kick (see type remarks).
        return currentZoneOwningFactionAlly(owningFaction) == 0;
    }
}
