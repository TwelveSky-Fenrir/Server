using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Domain;

/// <summary>
///     Bound from the <c>Game</c> configuration section. One process = one shard hosting the disjoint set of maps
///     assigned to <see cref="ShardId" />.
/// </summary>
public sealed class GameServerOptions
{
    public int Port { get; set; } = 1100;

    /// <summary>
    ///     Identifies this process's row in <c>runtime.GameServerDirectory</c> and the key looked up in
    ///     <c>admin.ShardMapAssignments</c>.
    /// </summary>
    public byte ShardId { get; set; } = 1;

    /// <summary>
    ///     Dev-only NTFS junction onto the legacy DATA tree (not committed -- multi-hundred-MB external asset). Resolved
    ///     against cwd, not <c>AppContext.BaseDirectory</c>.
    /// </summary>
    public string GameDataDirectory { get; set; } = "GameData";

    /// <summary>Advertised to LoginServer via the directory heartbeat; not a client-visible legacy value.</summary>
    public string PublicHost { get; set; } = "127.0.0.1";

    public int TickRateHz { get; set; } = 20;

    /// <summary>Interest-management cell size (view radius).</summary>
    public float AoiCellSize { get; set; } = 75f;

    /// <summary>
    ///     Anti-speed-hack budget. No legacy source documents an exact speed for M1's map, so this is a generous
    ///     placeholder, not real game-balance tuning.
    /// </summary>
    public float MaxPlausibleSpeedPerSecond { get; set; } = 20f;

    /// <summary>How often this shard refreshes its <c>runtime.GameServerDirectory</c> heartbeat row.</summary>
    public int HeartbeatIntervalSeconds { get; set; } = 5;

    /// <summary>
    ///     How often this shard polls whether the weekly hero-ranking Current-&gt;Previous rollover
    ///     (<c>game.usp_HeroRanking_Rollover</c>) is due. The proc itself gates on a 7-day sentinel and is
    ///     safe to call redundantly from every shard, so an hourly poll is plenty -- this is a detection
    ///     cadence, not the rollover period itself.
    /// </summary>
    public int HeroRankingRolloverCheckIntervalMinutes { get; set; } = 60;

    /// <summary>Informational only in M1, not enforced as a hard connection cap.</summary>
    public int Capacity { get; set; } = 300;

    /// <summary>
    ///     How often <c>AccountSessionKickPollHost</c> polls whether any account this shard holds a live Zone
    ///     session for has been flagged for kick in <c>runtime.AccountSessions</c> (a newer login elsewhere).
    /// </summary>
    public int AccountSessionPollIntervalSeconds { get; set; } = 20;

    /// <summary>
    ///     Trigger A of <c>Fenrir.Network.Dispatch.FloodProtection.IpFloodGuard</c>: max concurrent connections
    ///     from one IP to this shard before it's persistently blocked (<c>admin.FirewallRules</c>). Legacy
    ///     hardcodes this to 40 as a compile-time <c>#define</c> (<c>MAX_CONNECTIONS_PER_IP</c>,
    ///     <c>Server/Header/enter_count.cpp:5</c>); Fenrir makes it operator-configurable instead, per that
    ///     feature's own explicit "strict improvement" mandate.
    /// </summary>
    public int MaxConnectionsPerIp { get; set; } = 40;

    /// <summary>
    ///     Trigger B of <c>Fenrir.Network.Dispatch.FloodProtection.IpFloodGuard</c>: max unrecognized-opcode
    ///     protocol violations from one IP within a rolling wall-clock hour before it's persistently blocked.
    ///     Legacy hardcodes this to 30 (<c>MAX_ERROR_PACKET_PER_IP</c>, <c>Server/Header/enter_count.cpp:6</c>),
    ///     but legacy's own escalation function has been dead code (no live call site) since before the current
    ///     unknown-opcode handling was written -- see <see cref="MaxConnectionsPerIp" />'s sibling type's own
    ///     remarks. This threshold is therefore NEW behavior beyond legacy parity, not a reproduction of a
    ///     currently-live legacy limit.
    /// </summary>
    public int MaxProtocolViolationsPerIpPerHour { get; set; } = 30;

    /// <summary>
    ///     Legacy per-instance <c>Zone.Server</c> INI key <c>VoteTribe</c> (Server/Header/ini.h:308-310): arms
    ///     <c>Fenrir.Application.Game.Hosting.World.ZoneWar.TribeVoteElectionCalendarHost</c> on whichever live
    ///     shard currently hosts <see cref="VoteTribeMapId" /> -- every other shard never runs that scheduler,
    ///     regardless of this flag. Legacy gates the equivalent on the single physical instance running server
    ///     number 37 (Server/ts25zone/S07_MyGame01.cpp:612-616); Fenrir shards by map, not by a numbered
    ///     server-instance-per-process, so "the shard hosting the designated map" is the natural translation.
    /// </summary>
    public bool VoteTribeEnabled { get; set; }

    /// <summary>
    ///     The map id this shard must host for <see cref="VoteTribeEnabled" /> to actually arm
    ///     <c>TribeVoteElectionCalendarHost</c> -- see that flag's own remarks. Legacy's designated server
    ///     number for this content is 37 (Server/ts25zone/S07_MyGame01.cpp:612-616); an operator configuring
    ///     the real world should set this to whichever map id that content lives on. 0 (never a real hosted
    ///     map) by default, matching <see cref="HolyStoneMapId" />'s own fail-safe convention.
    /// </summary>
    public short VoteTribeMapId { get; set; }

    /// <summary>
    ///     Legacy per-instance INI key <c>VoteTribeTest</c> (Server/Header/ini.h:308-310): when set alongside
    ///     <see cref="VoteTribeEnabled" />, the legacy's own hour-compressed test calendar is entirely
    ///     commented-out dead code (Server/ts25zone/S07_MyGame01.cpp:3583-3629), so the faithful behavior is a
    ///     complete no-op -- <see cref="Hosting.World.ZoneWar.TribeVoteElectionCalendarHost" /> never advances
    ///     the election cycle while this is true.
    /// </summary>
    public bool VoteTribeTestMode { get; set; }

    /// <summary>
    ///     Legacy per-instance <c>Zone.Server</c> INI key <c>AllianceTribe</c> (Server/Header/ini.h:308): will
    ///     arm the alliance diplomacy ceremony (Server/ts25zone/S07_MyGame01.cpp:3764-4012's
    ///     <c>Process_Allience_Server</c>) on whichever live shard currently hosts
    ///     <see cref="AllianceTribeMapId" /> -- same map-id-keyed gate as <see cref="VoteTribeEnabled" />, once
    ///     a driver exists. Not yet consumed by any Hosting scheduler -- see
    ///     <c>Fenrir.Application.Game.Domain.World.ZoneWar.AllianceDiplomacyCeremony</c>'s own remarks for why
    ///     the per-tick driver is not wired up yet.
    /// </summary>
    public bool AllianceTribeEnabled { get; set; }

    /// <summary>
    ///     The map id this shard must host for <see cref="AllianceTribeEnabled" /> to arm the (not yet built)
    ///     alliance diplomacy ceremony driver. Legacy's designated server number for this content is 37
    ///     (same physical instance as <see cref="VoteTribeMapId" />/<see cref="TribeSymbolBattleMapId" />),
    ///     but each of these three systems gets its own independently-configurable map id rather than one
    ///     shared field -- the three sharing a server number in legacy is an accident of its
    ///     one-process-per-map architecture, not a game rule Fenrir must hard-code. 0 by default.
    /// </summary>
    public short AllianceTribeMapId { get; set; }

    /// <summary>
    ///     Map ids this shard's zones run the Zone-241 "LOD" personal-boss-chain dungeon content on. Legacy
    ///     arms this per-process at boot from <c>mSERVER_INFO.mServerNumber</c> falling in 325-330
    ///     (Server/ts25zone/S07_MyGame03.cpp:5946-5960,6240-6242; Server/ts25zone/S04_MyWork02.cpp:1143) --
    ///     Fenrir shards by map, not by a numbered server-instance-per-process, so this operator-configured
    ///     map-id set is the natural translation of that same one-time-at-boot gate. Empty by default: the
    ///     content is entirely inert (the per-avatar instance struct still exists on every
    ///     <see cref="World.PlayerRuntimeState" /> but is never armed) until an operator lists a map id here.
    /// </summary>
    public ISet<short> Zone241DungeonMapIds { get; set; } = new HashSet<short>();

    /// <summary>
    ///     Legacy per-instance <c>Zone.Server</c> INI flag arming Zone037's scheduled Tribe Symbol Battle
    ///     open/close cycle (<c>Fenrir.Application.Game.Hosting.World.ZoneWar.TribeSymbolBattleSchedulerHost</c>)
    ///     on whichever live shard currently hosts <see cref="TribeSymbolBattleMapId" />
    ///     (Server/ts25zone/S07_MyGame01.cpp:578-622's legacy server-number-37 gate) -- the same designated
    ///     content as <see cref="VoteTribeEnabled" />/<see cref="AllianceTribeEnabled" />, just a distinct
    ///     feature flag and a distinct map-id field; every other shard never runs that scheduler regardless
    ///     of this flag.
    /// </summary>
    public bool HolyStoneBattleEnabled { get; set; }

    /// <summary>
    ///     The map id this shard must host for <see cref="HolyStoneBattleEnabled" /> to actually arm
    ///     <c>TribeSymbolBattleSchedulerHost</c> -- see that flag's own remarks. 0 by default.
    /// </summary>
    public short TribeSymbolBattleMapId { get; set; }

    /// <summary>
    ///     Legacy per-instance INI flag arming Zone038's Holy Stone possession-war cycle
    ///     (<c>Fenrir.Application.Game.Hosting.World.ZoneWar.HolyStoneWarCycleHost</c>) on whichever live shard
    ///     currently hosts <see cref="HolyStoneMapId" /> (Server/ts25zone/S07_MyGame01.cpp:793-819's legacy
    ///     server-number-38 gate) -- <see cref="HolyStoneMapId" /> is dual-purpose, both this arm gate and the
    ///     war cycle's own geometry/participation-radius site identity, rather than a second, independently
    ///     configurable "which shard runs this" fact that could drift from the first.
    /// </summary>
    public bool HolyStoneWarEnabled { get; set; }

    /// <summary>
    ///     Shared "server test mode" bypass the translated behavior contract describes identically for both
    ///     Zone037 (skips the day-of-week gate; the fixed-hour gate still applies) and Zone038 (shortens the
    ///     between-war cooldown from three simulated hours to one) -- modeled as one flag rather than two
    ///     distinctly-named ones since both citations use the same generic "server test mode configuration
    ///     flag" wording and live in the same source range (Server/ts25zone/S07_MyGame01.cpp:3632-4287).
    /// </summary>
    public bool HolyStoneTestMode { get; set; }

    /// <summary>
    ///     The three fixed days of the week Zone037 restricts its open/close schedule to outside
    ///     <see cref="HolyStoneTestMode" />. Not named by the translated behavior contract (only "one of three
    ///     fixed days" is stated) -- empty by default, which fails safe (the schedule simply never fires
    ///     outside test mode) rather than guessing which three days.
    /// </summary>
    public ISet<DayOfWeek> HolyStoneBattleDays { get; set; } = new HashSet<DayOfWeek>();

    /// <summary>
    ///     Zone038: the map id the Holy Stone itself sits on, plus its fixed location and the two contest/
    ///     participation radii around it -- and, per <see cref="HolyStoneWarEnabled" />'s own remarks, also the
    ///     arm gate for <c>HolyStoneWarCycleHost</c> (whichever live shard hosts this map runs the war cycle).
    ///     None of these are named by the translated behavior contract (only that a "fixed radius" and a "fixed
    ///     location" exist) -- MapId 0 (never a real hosted map) and zero-sized radii by default, so the war
    ///     cycle's contest/challenge phases simply find no zone/nobody in range to act on until an operator
    ///     configures the real values.
    /// </summary>
    public short HolyStoneMapId { get; set; }

    public float HolyStoneX { get; set; }
    public float HolyStoneZ { get; set; }
    public float HolyStoneCaptureRadius { get; set; } = 1f;
    public float HolyStoneParticipationRadius { get; set; } = 1f;

    /// <summary>
    ///     Zone039: the fixed, small set of territory map ids around the Holy Stone this shard hosts, if any --
    ///     same "Fenrir shards by map, not by a numbered server instance" translation as
    ///     <see cref="Zone241DungeonMapIds" />. Empty by default: the eviction sweep simply finds no zone to
    ///     scan until an operator lists the real map ids here.
    /// </summary>
    public ISet<short> HolyStoneTerritoryMapIds { get; set; } = new HashSet<short>();

    /// <summary>
    ///     Which per-map tribe-population quota rule this shard enforces on TEMP_REGISTER_SEND
    ///     (op11/ZoneHandshake) admission -- see <see cref="TribeQuotaGroup" />'s own remarks for the map-group
    ///     translation and citations. <c>None</c> by default: fails safe, no quota enforced, until an operator
    ///     declares which group this shard's hosted map(s) belong to.
    /// </summary>
    public TribeQuotaGroup TribeQuotaGroup { get; set; } = TribeQuotaGroup.None;

    /// <summary>
    ///     How often <c>TempRegistrationIdleSweepHost</c> polls for a TEMP_REGISTER_SEND (op11) connection that
    ///     never followed up with avatar-selection/ready within <see cref="TempRegistrationIdleSweep.IdleTimeout" />'s
    ///     three-minute window (Server/ts25zone/S07_MyGame01.cpp:1963-1990). A poll cadence, not the timeout
    ///     itself -- a real forced-disconnect never lags the three-minute cutoff by more than one cycle.
    /// </summary>
    public int TempRegistrationIdleSweepIntervalSeconds { get; set; } = 30;
}
