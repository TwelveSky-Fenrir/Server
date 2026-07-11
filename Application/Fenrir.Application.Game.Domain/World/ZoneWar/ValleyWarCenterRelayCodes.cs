namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Citation-only catalog for the Center-side half of the Valley of the Deceased (Zone 200/297/298/299)
///     mechanism: the numeric event-code range Center receives over <c>ZC_ZONE_BROADCAST_FOR_CENTER_SEND</c>
///     (opcode 33) for this family, and Center's own reaction to every code inside it -- NONE.
///     <para>
///         <b>No wiring change is needed anywhere for this range.</b> Every code 601-675 is either a bare
///         no-op case body or entirely absent from both the Center-receive and zone-receive dispatch tables in
///         the legacy source (see Réf. C++ below); the only observable effect of any code in this range is an
///         unconditional, content-blind relay of the exact same code and payload to every other connected
///         zone process, which each of those, in turn, relays unmodified to its own connected clients. Fenrir
///         already reproduces this exactly: <see cref="ZoneCenterBroadcastIngestor.ApplyStateEffect" />'s
///         <c>switch</c> has (correctly) no case label anywhere in 601-675, so it falls through with zero
///         state effect for every code in this range, and <see cref="ZoneCenterBroadcastIngestor.Ingest" />
///         unconditionally relays every event code regardless of whether a case matched
///         (<see cref="ZoneCenterBroadcastIngestor.Relay" />). <b>The absence of a case label for this range
///         IS the correct, verified behavior -- do not add one.</b> This class exists purely to give this
///         specific citation trail (narrower and more precise than that switch's own generic
///         "601-628... 659-675" comment) a permanent, discoverable home; nothing at runtime consumes it.
///     </para>
///     <para>
///         The one genuinely live sub-family inside this range -- the gate-countdown/gate-opened/gate-closed/
///         door-opened/tribe-win/battle-scroll-deleted/boss-defeated/return-to-town sequence a
///         <see cref="ValleyWarSchedule" /> instance actually drives once per zone tick -- is already fully
///         implemented and wired: the state machine lives in <see cref="ValleyWarSchedule" />, its per-zone
///         tick driver in <see cref="ValleyWarSystem" />, and its own eight live event codes (659, 660, 662,
///         663, 666, 667, 668, 669 -- 661/664/665 have no live call site for this family) are defined,
///         individually cited, and broadcast by <see cref="ZoneEventBroadcaster" />'s own
///         <c>AnnounceValleyWar*</c> methods, which is this range's single source of truth for those eight
///         values; nothing here redeclares them.
///     </para>
/// </summary>
/// <remarks>
///     <para>
///         Réf. C++ : Server/ts25center/S04_MyWork02.cpp:160,212,1013-1085 (the relay-receive handler and its
///         full case range for status codes 601-675; every case body confirmed by direct read to be a bare
///         no-op, including two case labels whose bodies are commented out and one case that falls through
///         into the next, equally empty, case) ; :1214-1221 (the unconditional post-switch relay: the same
///         status code and payload are forwarded to every currently connected zone-server-type user) ;
///         :1184-1211 (the only two case labels in the entire handler that exit early instead of falling into
///         that relay -- confirmed neither overlaps 601-675).
///     </para>
///     <para>
///         Server/ts25zone/S07_MyGame08.cpp:1092-1163 (the zone-side receive mirror of the same 601-675 range,
///         confirmed by direct read to be likewise all no-op case bodies, several carrying explicit source
///         comments documenting that shared memory already carries the real state) ; :1350-1352 (the
///         unconditional post-switch effect that delivers the relayed status code to every client connected to
///         that zone process -- the final leg of the whole relay chain).
///     </para>
///     <para>
///         Two clusters in this range have dedicated case labels on BOTH the Center-receive and zone-receive
///         dispatch tables, yet no call site anywhere in the supplied legacy findings ever sends any code in
///         either cluster: <see cref="GodIndex2ClusterStart" />-<see cref="GodIndex2ClusterEnd" /> ("Valkey of
///         Deceased ... index=2(God)") and <see cref="MonsterSiegeClusterStart" />-<see cref="MonsterSiegeClusterEnd" />
///         ("Monster Siege"). Their exact payload layout, the "index=2(God)" annotation's meaning, and whether
///         either cluster relates at all to the gate/door/kill/win mechanism above (beyond an overlapping
///         comment string) are UNRECOVERABLE from the cited source -- do not infer or invent a trigger or
///         payload for either cluster in any downstream implementation. Flag for
///         <c>cpp-zone-gameplay-analyst</c> re-check if a future product requirement needs this
///         "God index=2" / "Monster Siege" mechanism
///         specifically, as distinct from the gate/door/kill/win mechanism <see cref="ValleyWarSchedule" />
///         already ports.
///     </para>
///     <para>
///         Three further status codes in this range appear as case labels/comments in both dispatch tables
///         ("gate will close," a code following a fall-through pair, and "other clan lose, return to town")
///         but are never actually relayed by any code path anywhere found in the supplied legacy findings --
///         no call site anywhere passes these three literal values to the function that originates a relay.
///         Their intended trigger and payload meaning, AND their own numeric literal, cannot be recovered from
///         the cited source; per this project's "never invent a magnitude/id" rule they are deliberately given
///         no named constant here. Flag for <c>cpp-zone-gameplay-analyst</c> re-check before any
///         implementation ever needs to originate one of these three codes.
///     </para>
/// </remarks>
public static class ValleyWarCenterRelayCodes
{
    /// <summary>
    ///     Whole numeric range Center receives (opcode 33) for the Valley of the Deceased family, per the
    ///     relay-receive handler's own case range (Server/ts25center/S04_MyWork02.cpp:1013-1085).
    /// </summary>
    public const int RelayRangeStart = 601;

    public const int RelayRangeEnd = 675;

    /// <summary>"Valkey of Deceased ... index=2(God)" -- see class remarks. Never sent by any cited call site.</summary>
    public const int GodIndex2ClusterStart = 601;

    public const int GodIndex2ClusterEnd = 610;

    /// <summary>"Monster Siege" -- see class remarks. Never sent by any cited call site.</summary>
    public const int MonsterSiegeClusterStart = 611;

    public const int MonsterSiegeClusterEnd = 615;

    /// <summary>Whether <paramref name="eventCode" /> falls inside this family's whole Center-relay range.</summary>
    public static bool IsInRelayRange(int eventCode)
    {
        return eventCode is >= RelayRangeStart and <= RelayRangeEnd;
    }

    /// <summary>Whether <paramref name="eventCode" /> falls inside the never-sent "God index=2" cluster.</summary>
    public static bool IsGodIndex2Cluster(int eventCode)
    {
        return eventCode is >= GodIndex2ClusterStart and <= GodIndex2ClusterEnd;
    }

    /// <summary>Whether <paramref name="eventCode" /> falls inside the never-sent "Monster Siege" cluster.</summary>
    public static bool IsMonsterSiegeCluster(int eventCode)
    {
        return eventCode is >= MonsterSiegeClusterStart and <= MonsterSiegeClusterEnd;
    }
}
