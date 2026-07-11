using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Numeric event/sort codes and map-id gates the A12 "scheduled zone/center events" behavior contract
///     newly supplies, byte-exact and citable, that <see cref="ZoneCenterBroadcastIngestor" />/
///     <see cref="ZoneEventBroadcaster" /> did not already have a home for. Every value here is copied
///     verbatim from that contract's own citations -- nothing here is inferred or invented.
/// </summary>
/// <remarks>
///     Réf. C++ (via the A12 scheduled-events behavior contract, itself citing):
///     Server/ts25zone/S07_MyGame01.cpp:4177-4180 (case-38 send site) ; :825-833 (the five Zone039-arming
///     gated server numbers 39/144/145/313/74) ; :4290-4300 (server-74-only <c>ResetMonsterSummonState</c>
///     gate, confirmed as the ONLY reaction sub-step restricted to 74 specifically -- point 1's battle-state
///     flag write applies to all five gated servers, point 2's monster-summon reset applies to 74 alone).
///     Server/ts25center/S04_MyWork02.cpp:1151-1157 (case 4000's own second, distinct broadcast -- sort 1601,
///     always-zero-filled payload, sent BEFORE the unconditional relay tail re-sends the original 4000).
///     Server/ts25center/S07_MyGame01.cpp:199-238 (the autonomous daily-reset broadcast -- sort 1600,
///     zero-filled payload, center-originated, no inbound trigger).
/// </remarks>
public static class ScheduledZoneCenterEventCodes
{
    /// <summary>
    ///     Case 38 (Zone038 win). Already broadcast today via <see cref="ZoneEventBroadcaster.AnnounceZone038Winner" />
    ///     (hardcoded inline there as <c>38</c>) -- this constant exists only so the A12 wiring pass can
    ///     reference the same value without a second magic number, not to replace that existing literal.
    /// </summary>
    public const int Zone038WinEventCode = 38;

    /// <summary>
    ///     Case 4000's own SECOND broadcast (GAP 4 in <see cref="ZoneCenterBroadcastIngestor" />'s own class
    ///     remarks, now resolved by this contract): a distinct sort value, always a zero-filled 130-byte
    ///     payload, sent once before the unconditional relay tail re-broadcasts the original case-4000
    ///     event/payload a second time. Deliberately NOT the same constant as
    ///     <see cref="ZoneCenterBroadcastIngestor.PingEventCode" /> (4000) -- that class's
    ///     <c>BroadcastAllZonesPing</c> currently reuses <c>PingEventCode</c> as a documented placeholder for
    ///     this value; see this task's wiringManifest for the one-line fix.
    /// </summary>
    public const int HsbRewardFlagResetPingEventCode = 1601;

    /// <summary>
    ///     The autonomous daily-reset broadcast (case 1600): center-originated, fired once daily at 00:01
    ///     (server wall clock), zero-filled 130-byte payload, no inbound zone trigger.
    /// </summary>
    public const int DailyResetEventCode = 1600;

    /// <summary>
    ///     The one server number, out of the five above, that additionally runs
    ///     <c>ResetMonsterSummonState</c> -- confirmed narrower than the general arming gate (contract:
    ///     "Only when the receiving server is physical server number 74 specifically").
    /// </summary>
    public const short Zone039MonsterSummonResetMapId = 74;

    /// <summary>
    ///     The five physical server numbers gated to receive the live "Zone039 arming" reaction to case 38 --
    ///     translated to Fenrir map ids the same way every other numbered-server-instance gate in this
    ///     cluster already is (<c>GameServerOptions.Zone241DungeonMapIds</c>,
    ///     <c>HolyStoneTerritoryEvictionSweep</c>'s own territory-map-id set).
    /// </summary>
    public static readonly FrozenSet<short> Zone039ArmingGatedMapIds =
        new short[] { 39, 144, 145, 313, 74 }.ToFrozenSet();
}
