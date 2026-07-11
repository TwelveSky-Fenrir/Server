namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     The two remaining boot-time "shard-type" classifications from the legacy zone process, alongside the
///     already-modeled <see cref="IsZone241TypeZone" />: <see cref="IsZone126TypeZone" /> and
///     <see cref="IsZone039TypeZone" />. Both are pure, config-driven map-id lookups -- the same shape as
///     <see cref="IsZone241TypeZone" /> -- resolved once per property read with no per-tick cost.
/// </summary>
/// <remarks>
///     Réf. C++ : legacy arms each of these once per zone process at boot, from that process's own configured
///     <c>mSERVER_INFO.mServerNumber</c>:
///     <list type="bullet">
///         <item>
///             <c>mCheckZone126TypeServer</c> is set true only for server numbers 210-218
///             (Server/ts25zone/S07_MyGame01.cpp:895-913), false for every other value. Its one live consumer
///             is the CP-Gift-Card tail tier's base-rate knob (50 when Zone126-type, 25 otherwise --
///             Server/ts25zone/S07_MyGame05.cpp:3402), wired here via
///             <see cref="MonsterDropTailResolver.ResolveCpGiftCard" />.
///         </item>
///         <item>
///             <c>mCheckZone039TypeServer</c> is set true only for server numbers 39, 74, 144, 145, 313
///             (Server/ts25zone/S07_MyGame01.cpp:821-834), false for every other value. It gates a
///             <b>different</b> tail tier -- a mount/pet event drop (Server/ts25zone/S07_MyGame05.cpp:3197-3204,
///             inside the <c>__GOD__</c>-gated block at :3109) whose weight is raised by Zone038 victory and
///             further by Zone126-type -- and does <b>not</b> participate in the CP-Gift 50-vs-25 rate at all.
///             That tier is not yet ported (no Fenrir producer reads this property today); the classification is
///             modeled now so the mount/pet tier can consume it without re-deriving the gate later.
///         </item>
///     </list>
///     Both feature areas compile into the shipped build: the gating macros <c>ZONE126</c>/<c>ZONE039</c> are
///     both defined unconditionally at Server/ts25zone/H07_MyGame.h:15,7, outside any build-variant guard.
///     <para>
///         <b>Legacy server number vs Fenrir map id.</b> Fenrir shards by map id (<c>admin.ShardMapAssignments</c>),
///         not by the legacy numbered-server-per-process scheme, and nothing in this codebase correlates the two.
///         So -- exactly as <see cref="IsZone241TypeZone" /> already does for <c>mCheckZone241TypeServer</c> --
///         the classification is an operator-configured set of <b>map ids</b>
///         (<see cref="GameServerOptions.Zone126TypeMapIds" />/<see cref="GameServerOptions.Zone039TypeMapIds" />),
///         the natural translation of the legacy one-time-at-boot gate. The concrete
///         server-number-to-map-id mapping is a topology decision for the operator/receiving engineer, not a
///         legacy fact to bake in here; the enumerated server numbers above are documentation only. Both sets
///         are empty by default, so both flags are false on every shard until an operator populates them --
///         matching legacy's own "unrecognized server number leaves both flags in their initialized-false
///         state" no-error fall-through (Server/ts25zone/S07_MyGame01.cpp:822,896).
///     </para>
/// </remarks>
public sealed partial class Zone
{
    /// <summary>
    ///     Whether this shard is configured as a "Zone-126-type" shard -- the Fenrir equivalent of legacy's
    ///     boot-time <c>mCheckZone126TypeServer</c> arm. Drives the CP-Gift-Card tail tier's base rate
    ///     (50 vs 25). See this file's own remarks for the server-number-to-map-id translation.
    /// </summary>
    public bool IsZone126TypeZone => options.Zone126TypeMapIds.Contains(MapId);

    /// <summary>
    ///     Whether this shard is configured as a "Zone-039-type" shard -- the Fenrir equivalent of legacy's
    ///     boot-time <c>mCheckZone039TypeServer</c> arm. Gates the (not-yet-ported) mount/pet event drop tier,
    ///     NOT the CP-Gift rate. See this file's own remarks.
    /// </summary>
    public bool IsZone039TypeZone => options.Zone039TypeMapIds.Contains(MapId);

    /// <summary>
    ///     Whether this shard is configured as dungeon-flagged -- the Fenrir equivalent of legacy's boot-time
    ///     <c>mIsDungeon</c> process flag (Server/Header/api.h:76, armed per
    ///     <c>Server/ts25zone/S02_MyServer.cpp:30-149</c>). Drives
    ///     <see cref="Monsters.DungeonSpawnDensityPolicy" />'s field-monster spawn-count density bump and
    ///     whole-table capacity-ceiling doubling, consumed by
    ///     <see cref="Monsters.MonsterSpawnScheduler.BuildState" />. See
    ///     <see cref="GameServerOptions.DungeonServerMapIds" />'s own remarks for why this is a distinct
    ///     config knob from <see cref="GameServerOptions.Zone241DungeonMapIds" />.
    /// </summary>
    public bool IsDungeonServerZone => options.DungeonServerMapIds.Contains(MapId);
}
