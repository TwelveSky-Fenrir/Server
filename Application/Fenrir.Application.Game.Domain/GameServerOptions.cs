using Fenrir.Application.Game.Domain.World;
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
    ///     Path to an operator-provisioned, gitignored external asset tree. May be ABSOLUTE (recommended -- resolved
    ///     as-is regardless of cwd, since <c>Path.Combine</c> drops earlier segments when a later one is rooted) or
    ///     cwd-relative. Only <c>{GameDataDirectory}/WORLD/Z{mapId:D3}.WM</c> is read from here -- each zone's
    ///     collision/navmesh geometry (<see cref="World.Zone.Geometry" />). Monster SPAWN data is NOT read from here:
    ///     it comes from <c>world.MonsterSpawnRegions</c> (DB, imported from the legacy <c>*.WREGION.csv</c>; the sibling
    ///     binary <c>.WREGION</c> files are legacy dead code -- the shipped build compiles the CSV reader,
    ///     <c>S10_MySummon.cpp:420</c>). See <see cref="GameServerOptionsValidator" /> for why a missing .WM is a
    ///     non-fatal boot warning (Program.cs rolls up which hosted maps lack navmesh) rather than a startup block.
    ///     PENDING (plan A1/A2): dungeon/instance maps share a canonical mesh via the legacy <c>mSameSummon</c>/<c>LoadWM</c>
    ///     physical-&gt;canonical zone remap (<c>S09_MyWorld.cpp:96-368</c>, plus the <c>_FIX</c> prefix for zones
    ///     39/144/145/313/74), not yet applied in <see cref="World.Zone" />'s geometry load path.
    /// </summary>
    public string GameDataDirectory { get; set; } = "GameData";

    /// <summary>Advertised to LoginServer via the directory heartbeat; not a client-visible legacy value.</summary>
    public string PublicHost { get; set; } = "127.0.0.1";

    /// <summary>
    ///     Time-to-live, in seconds, for the destination-shard-scoped <c>runtime.SessionTickets</c> row
    ///     <c>Fenrir.Application.Game.Services.ZoneLifecycle.ZoneMoveService</c> mints via
    ///     <c>ISessionTicketRepository.CreateAsync</c> when a zone-move request resolves to a map hosted by a
    ///     different live shard than this one -- the GameServer-side counterpart of
    ///     <c>Fenrir.Application.Login.Domain.LoginServerOptions.TicketTtlSeconds</c> (same ticket-row shape,
    ///     just minted by the other process for the reverse-direction, Game-&gt;Game handoff instead of
    ///     Login-&gt;Game). Same default as the Login-side setting: long enough for an immediate client
    ///     reconnect to the destination shard, short enough that an abandoned handoff never leaves a stale
    ///     row lingering.
    /// </summary>
    public int TicketTtlSeconds { get; set; } = 15;

    public int TickRateHz { get; set; } = 20;

    /// <summary>
    ///     Interest-management (area-of-interest) grid base cell size, in world units -- also the base
    ///     (scale-1) exact-distance radius <see cref="World.AoiGrid" />'s two-stage query overloads check
    ///     against (see that class's own remarks). Defaults to legacy's own base unit radius, 1000
    ///     (<c>MAX_RADIUS_FOR_NETWORK</c>, Server/Header/Protocol/DEFINE.h:600), which is also legacy's "scale 1"
    ///     visibility span (<c>UNIT_SCALE_RADIUS1</c>, DEFINE.h:141-143) -- the one every avatar and ordinary
    ///     ground item broadcast always uses (independently verified at Server/ts25zone/S04_MyWork04.cpp:1351,
    ///     Server/ts25zone/S07_MyGame04.cpp:2754, Server/ts25zone/S07_MyGame06.cpp:99-107) and the one the vast
    ///     majority of "ordinary" monsters fall through to as well (<c>ReturnSpecialSortNumber</c>'s
    ///     unconditional default, Server/ts25zone/S10_MySummon.cpp:646).
    ///     <para>
    ///         <see cref="World.AoiGrid" /> now implements both of legacy's visibility-check stages (coarse
    ///         per-axis cell box, then an exact 3D squared-distance check against the scaled radius,
    ///         Server/ts25zone/S07_MyGame03.cpp:796-856's <c>Broadcast11</c>) and the per-monster-category scale
    ///         widening to 2x/3x this radius (<see cref="World.Monsters.MonsterBroadcastScale" />, resolved from
    ///         <c>SendSpecialNumber</c>'s dispatch, Server/ts25zone/S07_MyGame05.cpp:3967-4001, and
    ///         <c>ReturnSpecialSortNumber</c>'s mapping, Server/ts25zone/S10_MySummon.cpp:612-647) for the one
    ///         broadcast family with an unambiguous single legacy call site (the periodic monster catch-up tick,
    ///         S07_MyGame01.cpp:2566) -- see that class's own remarks for the full mapping and for why the
    ///         monster-spawn announcement deliberately reuses the same mapping rather than modeling each
    ///         legacy summon call site's own separately-choosable dispatch strategy.
    ///     </para>
    ///     <para>
    ///         The coarse box partition itself still covers X/Z only, never Y -- this is retained deliberately,
    ///         not as an open question: see <see cref="World.AoiGrid" />'s own remarks for why the exact-distance
    ///         pass makes this harmless (the coarse box can only ever be a superset of legacy's own 3-axis
    ///         coarse candidate set, and the exact pass trims every survivor down to the true 3D radius either
    ///         way, so the final recipient set matches legacy exactly).
    ///     </para>
    /// </summary>
    public float AoiCellSize { get; set; } = 1000f;

    /// <summary>
    ///     Anti-teleport backstop: the maximum straight-line 3D distance a single accepted move
    ///     (CZ_AVATAR_ACTION_SEND / op15) may jump from the player's last accepted position, in world units.
    ///     Default 666 -- the sole numeric threshold that ever existed in the legacy op15 handler's DISABLED
    ///     "# Defense Hack #" block (<c>Server/ts25zone/S04_MyWork02.cpp:1741</c>: <c>fRange > 666.0f</c> where
    ///     <c>fRange = mUTIL.ReturnLengthXYZ(...)</c>, the full 3-axis distance -- ReturnLengthXYZ is 3D,
    ///     <c>Server/ts25zone/S07_MyGame03.cpp:5040-5043</c>). Legacy shipped that entire block commented out
    ///     (<c>S04_MyWork02.cpp:1738-1768</c>; corroborated <c>ServerDocs/12_ts25zone/04_MyWork02_PartieA.md:150-156</c>),
    ///     so legacy movement is fully client-authoritative with NO server speed/position check at all. Fenrir
    ///     reinstates only that one cited 666-unit ceiling and its 3D formula as an active backstop against gross
    ///     teleport hacks -- applied per consecutive accepted move (see MovementRules for why this corrects, and
    ///     is safer than, legacy's stale-<c>mPRE_LOCATION</c> reference), strictly more protective than legacy and
    ///     never less permissive for legitimate play (666 units is roughly ten times an ordinary walk step, so
    ///     normal movement never trips it). This deliberately replaces the earlier Fenrir-invented units/second
    ///     budget, which at the real world-coordinate scale (a zone spans thousands of units) rejected ordinary
    ///     walking and snapped legitimate clients back. Set to a very large value to effectively disable it
    ///     (closest to raw legacy behavior).
    /// </summary>
    public float MaxPlausibleMoveDistance { get; set; } = 666f;

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
    ///     How often <c>MuteRefreshPollHost</c> re-batches <c>admin.Mutes</c> for every online character on this
    ///     shard and applies the result to <c>PlayerRuntimeState.IsMuted</c> -- the bounded-staleness
    ///     approximation of legacy's per-message live mute recheck (see that host's own remarks). A GM
    ///     mute/unmute applied mid-session takes effect within this many seconds instead of only at the
    ///     character's next world entry.
    /// </summary>
    public int MutePollIntervalSeconds { get; set; } = 15;

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
    ///     Legacy per-instance <c>Zone.Server</c> INI key <c>AllianceTribe</c> (Server/Header/ini.h:308): arms
    ///     <c>Fenrir.Application.Game.Hosting.World.ZoneWar.AllianceDiplomacyCeremonyHost</c> (the alliance
    ///     diplomacy ceremony driver, Server/ts25zone/S07_MyGame01.cpp:3764-4012's
    ///     <c>Process_Allience_Server</c>) on whichever live shard currently hosts
    ///     <see cref="AllianceTribeMapId" /> -- same map-id-keyed gate as <see cref="VoteTribeEnabled" />.
    /// </summary>
    public bool AllianceTribeEnabled { get; set; }

    /// <summary>
    ///     The map id this shard must host for <see cref="AllianceTribeEnabled" /> to arm
    ///     <c>AllianceDiplomacyCeremonyHost</c>. Legacy's designated server number for this content is 37
    ///     (same physical instance as <see cref="VoteTribeMapId" />/<see cref="TribeSymbolBattleMapId" />),
    ///     but each of these three systems gets its own independently-configurable map id rather than one
    ///     shared field -- the three sharing a server number in legacy is an accident of its
    ///     one-process-per-map architecture, not a game rule Fenrir must hard-code. 0 by default.
    /// </summary>
    public short AllianceTribeMapId { get; set; }

    /// <summary>
    ///     Alliance ceremony post 0's fixed world-space X/Z location, on the map <see cref="AllianceTribeMapId" />
    ///     is configured to. Unlike <see cref="HolyStoneX" />/<see cref="HolyStoneZ" /> (defaulted to zero
    ///     because the translating contract never found their real values), these default to the actual
    ///     cited legacy values -- <c>mAllienceBattlePostLocation[0]</c>, Server/ts25zone/H07_MyGame.h:138-139
    ///     combined with Server/ts25zone/S07_MyGame01.cpp:593-595's <c>Init()</c> assignment: (-41, 8, -272).
    ///     Y is intentionally not exposed here -- see <see cref="World.ZoneWar.AlliancePostOccupantScanner" />'s
    ///     own remarks for why the post-occupant scan is X/Z-distance-only, the same documented divergence
    ///     from legacy's full 3D <c>CheckInRange</c> that <see cref="AoiCellSize" /> already flags elsewhere.
    /// </summary>
    public float AlliancePost0X { get; set; } = -41f;

    public float AlliancePost0Z { get; set; } = -272f;

    /// <summary>
    ///     Alliance ceremony post 1's fixed world-space X/Z location -- <c>mAllienceBattlePostLocation[1]</c>,
    ///     Server/ts25zone/S07_MyGame01.cpp:596-598: (35, 8, -272). See <see cref="AlliancePost0X" />'s own
    ///     remarks.
    /// </summary>
    public float AlliancePost1X { get; set; } = 35f;

    public float AlliancePost1Z { get; set; } = -272f;

    /// <summary>
    ///     Shared detection radius for both alliance ceremony posts -- <c>mAllienceBattlePostRadius[0]</c>
    ///     and <c>[1]</c>, Server/ts25zone/S07_MyGame01.cpp:599-600, both literally 10.0.
    /// </summary>
    public float AlliancePostRadius { get; set; } = 10f;

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

    /// <summary>
    ///     How often <c>SessionLivenessSweepHost</c> polls every currently-registered GameServer connection for
    ///     <see cref="SessionLivenessSweep.IdleTimeout" />'s three-minute no-successfully-dispatched-frame
    ///     window (Server/ts25zone/S07_MyGame01.cpp:1963-2006). A poll cadence, not the timeout itself -- a real
    ///     forced-disconnect never lags the three-minute cutoff by more than one cycle. Same default cadence as
    ///     <see cref="TempRegistrationIdleSweepIntervalSeconds" />, a sibling idle-connection sweep.
    /// </summary>
    public int SessionLivenessSweepIntervalSeconds { get; set; } = 30;

    /// <summary>
    ///     Legacy per-instance "JonNangin" INI-configured feature toggle
    ///     (Server/ts25zone/S04_MyWork02.cpp:7570-7575, wired from Server/Header/ini.h:328/api.h:157 per the
    ///     translating behavior contract) gating the fourth-tribe (Fujin) conversion/return behavior
    ///     (CZ_CHANGE_TO_TRIBE4_SEND, op37) -- a deployment-wide switch, not per-player/per-character. Defaults
    ///     false: this behavior was never live in the shipped legacy binary (its handler unconditionally
    ///     disconnected the client before any of this logic ran), so it fails safe/inert until an operator
    ///     deliberately opts in, matching this codebase's own convention for other newly-completed, previously
    ///     dead/unreachable content flags (<see cref="VoteTribeEnabled" />/<see cref="AllianceTribeEnabled" />/
    ///     <see cref="HolyStoneBattleEnabled" />).
    /// </summary>
    public bool TribeFourConversionEnabled { get; set; }

    /// <summary>
    ///     How often <c>GuildTribeBroadcastRelayHost</c> both flushes this shard's own queued outbound
    ///     GuildAnnouncement/GuildChat/TribeAnnouncement/TribeAnnouncementScroll broadcasts to
    ///     <c>runtime.GuildTribeBroadcastRelay</c> and polls that same table for broadcasts published by every
    ///     OTHER live shard, delivering matches to this shard's own locally-hosted players -- Fenrir's
    ///     SQL-mediated stand-in for legacy's <c>ts25zone</c>&lt;-&gt;<c>ts25center</c> relay uplink (opcodes
    ///     55-57 riding the same persistent connection, Server/ts25zone/H06_MyUpperCom.h:380,
    ///     Server/Header/Protocol/RELAY.h:6-29), since Fenrir has no <c>ts25center</c>-equivalent process. This
    ///     is a NEW, Fenrir-internal operational knob, not a reproduction of a legacy-cited value -- the
    ///     same-shard half of every one of these four opcodes' delivery is unaffected and stays
    ///     synchronous/immediate; only the OTHER-shard fan-out lags by up to this many seconds.
    /// </summary>
    public int GuildTribeBroadcastPollIntervalSeconds { get; set; } = 2;

    /// <summary>
    ///     How long a row survives in <c>runtime.GuildTribeBroadcastRelay</c> before
    ///     <c>GuildTribeBroadcastRelayHost</c>'s own poll cycle reaps it, regardless of whether every live
    ///     shard has already consumed it -- a crashed/never-polling shard simply misses broadcasts older than
    ///     this window rather than pinning the table's memory-optimized storage forever (same "safe direction
    ///     to fail toward" posture as every other SCHEMA_ONLY <c>runtime.*</c> table). Comfortably wider than
    ///     <see cref="GuildTribeBroadcastPollIntervalSeconds" />'s default so a normally-alive shard never
    ///     races its own poll cadence.
    /// </summary>
    public int GuildTribeBroadcastRetentionSeconds { get; set; } = 30;

    /// <summary>
    ///     How often <c>SocialCrossShardRelayHost</c> both flushes this shard's own queued outbound
    ///     Party invite/Friend ask/Mentor ask/Duel ask/Trade invite/Guild invite Ask+Answer rows to
    ///     <c>runtime.SocialCrossShardRelay</c> and polls that same table for rows addressed to THIS shard
    ///     (<c>TargetShardId = ShardId</c>), delivering matches to whichever registered
    ///     <c>ISocialCrossShardRelayHandler</c> owns that row's <c>Kind</c> -- the point-to-point sibling of
    ///     <see cref="GuildTribeBroadcastPollIntervalSeconds" />'s fan-out relay; see
    ///     <c>ISocialCrossShardRelayRepository</c>'s own remarks for the point-to-point-vs-fan-out
    ///     distinction. Also a NEW, Fenrir-internal operational knob, not a reproduction of a legacy-cited
    ///     value (legacy's single-process <c>ts25zone</c> had no cross-shard concept at all): the same-shard
    ///     half of every one of these six negotiation flows is unaffected and stays synchronous/immediate;
    ///     only the cross-shard leg lags by up to this many seconds. Tighter default than
    ///     <see cref="GuildTribeBroadcastPollIntervalSeconds" /> because a negotiation prompt (party/duel/
    ///     trade invite) is a synchronous, latency-sensitive human interaction in a way a chat broadcast is
    ///     not.
    /// </summary>
    public int SocialCrossShardRelayPollIntervalSeconds { get; set; } = 1;

    /// <summary>
    ///     How long a row survives in <c>runtime.SocialCrossShardRelay</c> before
    ///     <c>SocialCrossShardRelayHost</c>'s own poll cycle reaps it, regardless of whether its target shard
    ///     has already consumed it -- same "safe direction to fail toward" posture as
    ///     <see cref="GuildTribeBroadcastRetentionSeconds" />, and the same default value: comfortably wider
    ///     than <see cref="SocialCrossShardRelayPollIntervalSeconds" /> so a normally-alive shard never races
    ///     its own poll cadence.
    /// </summary>
    public int SocialCrossShardRelayRetentionSeconds { get; set; } = 30;

    /// <summary>
    ///     How often <c>ProxyShopExpirationRelayHost</c> both flushes this shard's own queued proxy-shop
    ///     rental-extension rows to <c>runtime.ProxyShopExpirationRelay</c> and polls that same table for
    ///     rows published by every OTHER live shard, applying matches to this shard's own locally-hosted
    ///     zone-37 <c>Zone</c> instance (if any) -- the fan-out sibling of
    ///     <see cref="GuildTribeBroadcastPollIntervalSeconds" />, hardening past a legacy structural
    ///     limitation rather than reproducing a legacy-cited value (legacy's own single-process
    ///     <c>ts25zone</c> registry update had no cross-shard concept at all -- see
    ///     <c>ProxyShopExpirationRelayHost</c>'s own remarks for the full trail). Same default as
    ///     <see cref="GuildTribeBroadcastPollIntervalSeconds" />: a rental-extension mirror update is not as
    ///     latency-sensitive as a live human negotiation prompt (<see cref="SocialCrossShardRelayPollIntervalSeconds" />'s
    ///     own tighter default), since the only thing it races is the periodic proxy-shop expiry sweep, not a
    ///     player waiting on a reply.
    /// </summary>
    public int ProxyShopExpirationRelayPollIntervalSeconds { get; set; } = 2;

    /// <summary>
    ///     How long a row survives in <c>runtime.ProxyShopExpirationRelay</c> before
    ///     <c>ProxyShopExpirationRelayHost</c>'s own poll cycle reaps it, regardless of whether every live
    ///     shard has already consumed it -- same "safe direction to fail toward" posture as
    ///     <see cref="GuildTribeBroadcastRetentionSeconds" />, and the same default value: comfortably wider
    ///     than <see cref="ProxyShopExpirationRelayPollIntervalSeconds" /> so a normally-alive shard never
    ///     races its own poll cadence.
    /// </summary>
    public int ProxyShopExpirationRelayRetentionSeconds { get; set; } = 30;

    /// <summary>
    ///     How often <c>RvrSiegeEventRelayHost</c> both flushes this shard's own queued outbound Zone049
    ///     siege-zone-slot (sub-codes 1-9) and tribe-symbol/alliance (tSort 38/39/40/42/45/46/47) rows to
    ///     <c>runtime.RvrSiegeEventRelay</c> and polls that same table for rows published by every OTHER live
    ///     shard, replaying matches through <c>ZoneCenterBroadcastIngestor.ApplyRelayedEvent</c>/
    ///     <c>ZoneEventBroadcaster.ApplyRelayedEvent</c> -- the fan-out sibling of
    ///     <see cref="GuildTribeBroadcastPollIntervalSeconds" />, hardening past the same "Fenrir shards
    ///     ZoneRegistry disjointly by map" structural gap <see cref="ProxyShopExpirationRelayPollIntervalSeconds" />'s
    ///     own remarks describe (legacy's single ts25center process had no such gap: it relayed to literally
    ///     every connected zone process). Same default as <see cref="GuildTribeBroadcastPollIntervalSeconds" />:
    ///     a siege-slot/tribe-symbol/alliance transition is schedule-cadence traffic, not as latency-sensitive
    ///     as a live human negotiation prompt (<see cref="SocialCrossShardRelayPollIntervalSeconds" />'s own
    ///     tighter default).
    /// </summary>
    public int RvrSiegeEventRelayPollIntervalSeconds { get; set; } = 2;

    /// <summary>
    ///     How long a row survives in <c>runtime.RvrSiegeEventRelay</c> before <c>RvrSiegeEventRelayHost</c>'s
    ///     own poll cycle reaps it, regardless of whether every live shard has already consumed it -- same
    ///     "safe direction to fail toward" posture as <see cref="GuildTribeBroadcastRetentionSeconds" />, and
    ///     the same default value: comfortably wider than <see cref="RvrSiegeEventRelayPollIntervalSeconds" />
    ///     so a normally-alive shard never races its own poll cadence.
    /// </summary>
    public int RvrSiegeEventRelayRetentionSeconds { get; set; } = 30;

    /// <summary>
    ///     Map ids this shard hosts that run the Zone195 "Nok-San" solo stone-capture content --
    ///     <see cref="World.ZoneWar.RegularWarAfkTickSystem" />'s own AFK-enforcement gate only; the Nok-San
    ///     capture mechanic itself (candidate lock/hold-countdown/possession transfer,
    ///     Server/ts25zone/S07_MyGame01.cpp:8374-8459+) is a separate, not-yet-implemented feature -- adding a
    ///     map id here does NOT turn on stone-capture gameplay, it only arms the AFK counter's 10-unit
    ///     threshold and continuous-enforcement gate for that map. Empty by default, matching every other
    ///     operator-configured map-id set in this class (fails safe: no AFK enforcement on any map until
    ///     configured).
    /// </summary>
    public ISet<short> Zone195MapIds { get; set; } = new HashSet<short>();

    /// <summary>
    ///     Legacy per-instance ini flag <c>mYaoguaiHSB</c> (Server/ts25zone/S07_MyGame01.cpp:2622-2651): arms
    ///     the "monster symbol" (mYaoguaiHSB) attack-window notify -- once the neutral, monster-guarded battle
    ///     symbol has been held by the same tribe for <see cref="MonsterSymbolAttackNotifyDelayMinutes" />, a
    ///     single center-broadcast (sort 401, no payload) fires once. False by default: fails safe, no
    ///     broadcast until an operator opts in.
    /// </summary>
    public bool MonsterSymbolAttackNotifyEnabled { get; set; }

    /// <summary>
    ///     Legacy per-instance ini value <c>mYaoguaiHSBMiniute</c>: real minutes the current monster-symbol
    ///     holder must keep the symbol before the one-shot sort-401 notify fires. 0 by default (paired with
    ///     <see cref="MonsterSymbolAttackNotifyEnabled" />'s own false default, so a misconfigured "enabled but
    ///     0 minutes" state can never silently fire immediately -- see <see cref="GameServerOptionsValidator" />).
    /// </summary>
    public int MonsterSymbolAttackNotifyDelayMinutes { get; set; }

    /// <summary>
    ///     Legacy maps each of the 4 monster-symbol holder tribes to one specific zone server number (0-&gt;4,
    ///     1-&gt;9, 2-&gt;14, 3-&gt;143, Server/ts25zone/S07_MyGame01.cpp:2622-2651) -- only the zone process
    ///     matching the CURRENT holder's mapped instance ever proceeds past that gate, every other instance
    ///     silently skips. Fenrir shards by map, not by numbered server instance, so this is the operator-
    ///     configured tribeId-&gt;mapId translation of that same table; empty by default (fails safe: no
    ///     map ever matches, so the notify never fires, until an operator supplies all 4 entries).
    /// </summary>
    public IReadOnlyDictionary<byte, short> MonsterSymbolAttackNotifyMapIds { get; set; } =
        new Dictionary<byte, short>();

    /// <summary>
    ///     How often <c>GuildBuffExpiryRelayHost</c> both flushes this shard's own queued outbound
    ///     guild-buff-reserve-exhaustion pushes (<c>Hosting.Guilds.GuildBuffDecayHost</c>'s own edge-triggered
    ///     <c>gBuffTime &lt; 1</c> detections) to <c>runtime.GuildBuffExpiryRelay</c> and polls that same table
    ///     for pushes published by every OTHER live shard, delivering matches to this shard's own
    ///     locally-hosted players -- the fan-out sibling of <see cref="GuildTribeBroadcastPollIntervalSeconds" />
    ///     for this one, dedicated, non-opcode-driven push (Server/ts25center/S07_MyGame01.cpp:291-331's own
    ///     <c>BroadcastZone()</c> cluster-wide send). A NEW, Fenrir-internal operational knob, not a
    ///     reproduction of a legacy-cited value -- the same-shard half (<c>GuildBuffDecayHost</c>'s own
    ///     immediate, synchronous delivery to this shard's hosted zones) is unaffected; only the OTHER-shard
    ///     fan-out lags by up to this many seconds.
    /// </summary>
    public int GuildBuffExpiryRelayPollIntervalSeconds { get; set; } = 2;

    /// <summary>
    ///     How long a row survives in <c>runtime.GuildBuffExpiryRelay</c> before <c>GuildBuffExpiryRelayHost</c>'s
    ///     own poll cycle reaps it, regardless of whether every live shard has already consumed it -- same
    ///     "safe direction to fail toward" posture as <see cref="GuildTribeBroadcastRetentionSeconds" />, and
    ///     the same default value.
    /// </summary>
    public int GuildBuffExpiryRelayRetentionSeconds { get; set; } = 30;
}
