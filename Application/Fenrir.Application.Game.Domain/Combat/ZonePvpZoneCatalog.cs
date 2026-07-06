using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Combat;

/// <summary>
///     Per-zone open-enemy-tribe-PvP authorization flag (Blocker #2, C05-adjacent) -- reimplements the legacy
///     compile-time-baked 350-entry table <c>ZONEMAININFO::mZoneTribeInfo[zoneNumber-1][1]</c>, populated by a
///     giant hardcoded switch inside <c>ZONEMAININFO::Init()</c> (<c>Server/Header/S18_MyZoneInfo.cpp:9-393</c>,
///     default 0/disabled at line 18 for any zone id absent from the switch). This table is NOT database-driven,
///     NOT admin-editable, and never varies at runtime in the legacy server either -- it is baked into the .exe
///     at compile time, so a small static lookup here is the faithful translation, not an under-engineered
///     shortcut. See <see cref="CombatResolver.ResolveEnemyTribeAttack" />'s own
///     <c>zoneAllowsEnemyTribeAttack</c> parameter doc for the caller-side contract this catalog exists to fill.
/// </summary>
/// <remarks>
///     The legacy flag is tri-state (0/1/2), but both real call sites that read it
///     (<c>Server/ts25zone/S07_MyGame02.cpp:947</c>, <c>:3579</c>) only test equality-to-zero, so 1 and 2 collapse
///     to the same "enabled" outcome -- reduced to a bool here since no observed behavior distinguishes them.
///     <para>
///         Legacy zone ids outside <c>[MIN_VALID_ZONE_NUMBER, MAX_VALID_ZONE_NUMBER]</c> (1-350,
///         <c>Server/Header/Protocol/DEFINE.h:373-375</c>) make <c>ReturnZoneTribeInfo2</c> return -1
///         (<c>S18_MyZoneInfo.cpp:426-433</c>), which happens to also fail the callers' <c>== 0</c> guard and
///         therefore allow PvP by accident of that guard's shape. An out-of-range zone id is unreachable for a
///         live Fenrir <see cref="World.Zone" /> (its <c>MapId</c> always comes from a real <c>world.Zones</c> row
///         via <c>admin.ShardMapAssignments</c>), so <see cref="AllowsEnemyTribeAttack" /> deliberately fails
///         closed (not in the set = disabled) for any id outside 1-350 instead of reproducing that unreachable
///         fail-open edge case.
///     </para>
/// </remarks>
public static class ZonePvpZoneCatalog
{
    /// <summary>
    ///     Every zone id (1-350) where <c>mZoneTribeInfo[id-1][1]</c> is nonzero (1 or 2) in the legacy table,
    ///     extracted from the full switch statement at <c>Server/Header/S18_MyZoneInfo.cpp:9-393</c>. Any zone id
    ///     not listed here defaults to 0/disabled, matching the legacy's own default-initialization
    ///     (<c>S18_MyZoneInfo.cpp:18</c>) for every entry the switch doesn't explicitly assign.
    /// </summary>
    private static readonly FrozenSet<short> EnemyTribeAttackEnabledZoneIds = new short[]
    {
        1, 2, 3, 4, 6, 7, 8, 9, 11, 12, 13, 14, 38, 49, 51, 53, 54, 55, 75, 84, 85, 86, 87, 88, 89, 90, 99,
        100, 120, 121, 122, 125, 138, 139, 140, 141, 142, 143, 146, 147, 148, 149, 150, 151, 152, 153, 154,
        155, 156, 157, 158, 159, 160, 161, 162, 163, 164, 165, 166, 194, 195, 196, 197, 198, 199, 200, 201,
        250, 267, 268, 269, 270, 271, 272, 273, 274, 291, 295, 296, 297, 298, 299, 302, 303, 324, 335, 339,
        340, 344, 345
    }.ToFrozenSet();

    /// <summary>
    ///     True when open enemy-tribe PvP is authorized in <paramref name="zoneId" /> -- the value to pass as
    ///     <see cref="CombatResolver.ResolveEnemyTribeAttack" />'s <c>zoneAllowsEnemyTribeAttack</c> argument for
    ///     an attack occurring in this zone.
    /// </summary>
    public static bool AllowsEnemyTribeAttack(short zoneId)
    {
        return EnemyTribeAttackEnabledZoneIds.Contains(zoneId);
    }
}
