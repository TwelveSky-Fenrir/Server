using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Simulation;

/// <summary>
///     Legacy <c>POPUP_TYPE</c> (<c>Server/Header/Protocol/STRUCT.h:647-661</c>) -- the five kill-streak
///     popup-event categories, in the exact enum ordering the legacy world state and the wire
///     <c>WorldInfo.PopUpTypeState[5]</c> flag array are indexed by. Value order is load-bearing:
///     <see cref="PopupEventState" /> indexes its per-type on/off array by <c>(int)</c> this enum, matching
///     legacy's own <c>mPopUpTypeState[POPUP_TYPE]</c> indexing.
/// </summary>
public enum PopupEventType : byte
{
    /// <summary><c>REGULAR_WAR</c> = 0 -- war counter (<c>aPopUpKillAvtWar</c>), threshold 10.</summary>
    RegularWar = 0,

    /// <summary><c>YANGGOK_PVP</c> = 1 -- PvP-avatar counter (<c>aPopUpKillAvt</c>), threshold 10 (victim at level cap).</summary>
    YanggokPvp = 1,

    /// <summary><c>MONSTER_PVE</c> = 2 -- monster counter (<c>aPopUpKillMonster</c>), cap/threshold 400.</summary>
    MonsterPve = 2,

    /// <summary><c>INVASION_PVP</c> = 3 -- PvP-avatar counter (<c>aPopUpKillAvt</c>), threshold 5 (victim at level cap).</summary>
    InvasionPvp = 3,

    /// <summary><c>RUINS_PVP</c> = 4 -- war counter (<c>aPopUpKillAvtWar</c>), threshold 10.</summary>
    RuinsPvp = 4
}

/// <summary>
///     Per-type shard/map-id gating for the popup-event reward system, reproducing the five <c>IsPopUp*</c>
///     predicates (<c>Server/Header/function.h:3258-3344</c>). Each popup type only ever triggers on a fixed
///     set of server/shard numbers; in Fenrir's model that identity is the ticking <see cref="World.Zone.MapId" />
///     (the legacy predicates test <c>mSERVER_INFO.mServerNumber</c>, i.e. the process's own fixed assigned map
///     identity -- <c>Server/ts25zone/S07_MyGame03.cpp:2720-2723</c> -- never the victim's live map cell). The
///     five sets are pairwise disjoint, so a given map resolves to at most one popup type.
/// </summary>
/// <remarks>
///     The legacy <c>CheckForPopUpZone</c> (<c>H07_MyGame.h:592</c>, <c>S07_MyGame03.cpp:2508-2581</c>) is NOT
///     the live gate and is deliberately not reproduced: it has no call site anywhere under <c>Server/</c>
///     (grep-confirmed), only guards shards 160/164 against the RegularWar flag, and returns TRUE everywhere
///     else with its multi-zone body commented out. The live gating is these <c>IsPopUp*</c> predicates.
/// </remarks>
public static class PopupEventZoneCatalog
{
    /// <summary>War counter (RegularWar/Ruins) per-kill requirement (<c>tKillReq</c>, S07_MyGame03.cpp:2737-2748).</summary>
    public const int WarKillThreshold = 10;

    /// <summary>Yanggok PvP-avatar counter per-kill requirement (<c>tKillReq</c>, S07_MyGame03.cpp:2752-2757).</summary>
    public const int YanggokKillThreshold = 10;

    /// <summary>Invasion PvP-avatar counter per-kill requirement (<c>tKillReq</c>, S07_MyGame03.cpp:2758-2764).</summary>
    public const int InvasionKillThreshold = 5;

    /// <summary>
    ///     Monster counter hard cap / reward threshold (<c>aPopUpKillMonster</c>, S07_MyGame05.cpp:2236-2251):
    ///     stops incrementing at 400 and fires the reward at exactly 400, then resets.
    /// </summary>
    public const int MonsterKillThreshold = 400;

    /// <summary>
    ///     <c>LNW33</c> "TEMP TEST" shard: on shard 164 the war threshold is forced to 1 (reward on every kill),
    ///     S07_MyGame03.cpp:2737-2748. <c>LNW33</c> is active in the shipped ReleaseEU33/ReleaseM33 builds
    ///     (<c>ServerDocs/03_BUILD_VARIANTS_ET_PREPROCESSEUR.md</c>), so this is modeled for parity -- but 164 is
    ///     not itself a member of <see cref="RegularWarMaps" /> or <see cref="RuinsMaps" />, so this override is
    ///     currently inert (no war-type popup ever resolves on 164). Flagged for cpp-zone-gameplay-analyst
    ///     re-check in case 164 is separately configured as a war-popup test map.
    /// </summary>
    public const short LnwTestShard = 164;

    /// <summary><c>IsPopUpRegularWar</c>: 146 or 160 (<c>function.h:3258-3272</c>).</summary>
    private static readonly FrozenSet<short> RegularWarMaps = new short[] { 146, 160 }.ToFrozenSet();

    /// <summary><c>IsPopUpYanggok</c>: 38 (<c>function.h:3274-3288</c>).</summary>
    private static readonly FrozenSet<short> YanggokMaps = new short[] { 38 }.ToFrozenSet();

    /// <summary>
    ///     <c>IsPopUpMonster</c>: 104-109, 128, 129, 132, 133, 136, 137, 168, 169, 173, 174, 145
    ///     (<c>function.h:3290-3316</c>).
    /// </summary>
    private static readonly FrozenSet<short> MonsterMaps = new short[]
    {
        104, 105, 106, 107, 108, 109, 128, 129, 132, 133, 136, 137, 168, 169, 173, 174, 145
    }.ToFrozenSet();

    /// <summary><c>IsPopUpInvasion</c>: 1, 6, 11, 140 (<c>function.h:3318-3332</c>).</summary>
    private static readonly FrozenSet<short> InvasionMaps = new short[] { 1, 6, 11, 140 }.ToFrozenSet();

    /// <summary><c>IsPopUpRuins</c>: 268 (<c>function.h:3334-3344</c>).</summary>
    private static readonly FrozenSet<short> RuinsMaps = new short[] { 268 }.ToFrozenSet();

    /// <summary>
    ///     Resolves which PvP popup type (if any) applies to a <paramref name="mapId" />. Monster/PvE is NOT
    ///     returned here -- it is a distinct event kind, see <see cref="IsMonsterPopupMap" />. Returns at most one
    ///     type since the four PvP sets are pairwise disjoint.
    /// </summary>
    public static bool TryResolvePvpType(short mapId, out PopupEventType type)
    {
        if (RegularWarMaps.Contains(mapId))
        {
            type = PopupEventType.RegularWar;
            return true;
        }

        if (YanggokMaps.Contains(mapId))
        {
            type = PopupEventType.YanggokPvp;
            return true;
        }

        if (InvasionMaps.Contains(mapId))
        {
            type = PopupEventType.InvasionPvp;
            return true;
        }

        if (RuinsMaps.Contains(mapId))
        {
            type = PopupEventType.RuinsPvp;
            return true;
        }

        type = default;
        return false;
    }

    /// <summary><c>IsPopUpMonster</c>: whether <paramref name="mapId" /> is a Monster/PvE popup shard.</summary>
    public static bool IsMonsterPopupMap(short mapId)
    {
        return MonsterMaps.Contains(mapId);
    }

    /// <summary>
    ///     Per-type per-kill counting requirement (<c>tKillReq</c>). For the war-counter types the shard-164
    ///     <c>LNW33</c> override applies (see <see cref="LnwTestShard" />). Monster/PvE uses
    ///     <see cref="MonsterKillThreshold" /> and does not flow through here (its own reset semantics differ).
    /// </summary>
    public static int KillThreshold(PopupEventType type, short mapId)
    {
        return type switch
        {
            PopupEventType.RegularWar or PopupEventType.RuinsPvp =>
                mapId == LnwTestShard ? 1 : WarKillThreshold,
            PopupEventType.YanggokPvp => YanggokKillThreshold,
            PopupEventType.InvasionPvp => InvasionKillThreshold,
            PopupEventType.MonsterPve => MonsterKillThreshold,
            _ => WarKillThreshold
        };
    }

    /// <summary>Whether <paramref name="type" /> shares the war counter (<c>aPopUpKillAvtWar</c>).</summary>
    public static bool UsesWarCounter(PopupEventType type)
    {
        return type is PopupEventType.RegularWar or PopupEventType.RuinsPvp;
    }
}

/// <summary>
///     Process-wide per-type popup on/off state -- the in-memory model of legacy's <c>mPopUpTypeState[5]</c>
///     world flags (<c>Server/Header/Protocol/STRUCT.h:647-661</c>). These are external inputs to the reward
///     behavior: how they get toggled on/off is out of scope for
///     <see cref="PopupEventRewardSystem" /> (legacy toggles them via a center broadcast; Fenrir has no such
///     source wired yet -- see the workstream notes). Defaults to all-disabled, so the whole popup system is a
///     safe no-op until a flag is explicitly armed, matching legacy where a popup type never triggers unless its
///     flag is on.
/// </summary>
/// <remarks>
///     Read from every hosting zone's tick thread and written by whatever external toggler ends up owning it;
///     each flag is an independent <see cref="bool" /> accessed through <see cref="Volatile" /> so a toggle
///     becomes visible to the tick threads without a lock. No cross-flag invariant exists, so no coarser
///     synchronization is warranted.
/// </remarks>
public sealed class PopupEventState
{
    private readonly bool[] _enabled = new bool[5];

    public bool IsEnabled(PopupEventType type)
    {
        return Volatile.Read(ref _enabled[(int)type]);
    }

    public void SetEnabled(PopupEventType type, bool enabled)
    {
        Volatile.Write(ref _enabled[(int)type], enabled);
    }
}
