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
///     Also hosts <see cref="IsSameTribeAttackExempt" />, a second, structurally distinct per-zone table for the
///     zone-324/335 same-tribe-attack exemption -- see that method's own remarks for why it's a separate list.
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
    ///     Zone ids where the same-tribe/allied-tribe attack rejection inside <c>AttackPlayer</c>'s non-duel
    ///     ("ENEMY") branch is skipped entirely -- "any tribe may attack any tribe" open-PvP maps. Unlike
    ///     <see cref="EnemyTribeAttackEnabledZoneIds" /> above (the <c>mZoneTribeInfo</c> table read by
    ///     <see cref="AllowsEnemyTribeAttack" />), this is a separate, literal <c>!= 324 &amp;&amp; != FFAMAPNUM</c>
    ///     guard hardcoded directly in the same function, evaluated immediately after that gate already passes
    ///     (<c>Server/ts25zone/S07_MyGame02.cpp:952-958</c>; <c>FFAMAPNUM</c> =
    ///     <see cref="PvpKillRewardZoneCatalog.FfaMapNumber" />,
    ///     <c>Server/Header/Protocol/DEFINE.h:99</c>). Exactly these two zone ids and no others are exempted --
    ///     every other zone, including every other zone where <see cref="AllowsEnemyTribeAttack" /> is true,
    ///     still runs the ordinary same-tribe/allied-tribe rejection.
    /// </summary>
    private static readonly FrozenSet<short> SameTribeAttackExemptZoneIds =
        new short[] { 324, PvpKillRewardZoneCatalog.FfaMapNumber }.ToFrozenSet();

    /// <summary>
    ///     Zone ids where <c>AttackPlayer</c>'s ENEMY branch enforces a "newbie protection" level gate: an
    ///     attacker whose <c>aLevel1</c> is &gt;= 90 may not attack a defender whose <c>aLevel1</c> is &lt; 90
    ///     (<c>Server/ts25zone/S07_MyGame02.cpp:960-976</c>, the <c>switch (mSERVER_INFO.mServerNumber)</c>
    ///     immediately after the same-tribe/alliance check). These are the auxiliary districts of tribes 0/1/2's
    ///     four-zone home clusters (1-4/6-9/11-14 per <c>ZONEMAININFO::Init</c>'s tribe-assignment table,
    ///     <c>Server/Header/S18_MyZoneInfo.cpp:21-34</c>) -- the three "capital plaza" zones themselves (1, 6, 11)
    ///     are conspicuously absent from the switch's case list, so full enemy-tribe PvP applies there regardless
    ///     of either side's level.
    /// </summary>
    /// <remarks>
    ///     A structurally distinct table from both <see cref="EnemyTribeAttackEnabledZoneIds" /> (whether
    ///     enemy-tribe PvP is authorized here at all) and <see cref="SameTribeAttackExemptZoneIds" /> (whether the
    ///     same-tribe/allied-tribe rejection is skipped) -- this gate runs after both of those, only inside the
    ///     nine zones listed here, and never overlaps with <see cref="SameTribeAttackExemptZoneIds" />'s own two
    ///     entries (324, 335).
    /// </remarks>
    private static readonly FrozenSet<short> NewbieProtectionZoneIds =
        new short[] { 2, 3, 4, 7, 8, 9, 12, 13, 14 }.ToFrozenSet();

    /// <summary>
    ///     True when open enemy-tribe PvP is authorized in <paramref name="zoneId" /> -- the value to pass as
    ///     <see cref="CombatResolver.ResolveEnemyTribeAttack" />'s <c>zoneAllowsEnemyTribeAttack</c> argument for
    ///     an attack occurring in this zone.
    /// </summary>
    public static bool AllowsEnemyTribeAttack(short zoneId)
    {
        return EnemyTribeAttackEnabledZoneIds.Contains(zoneId);
    }

    /// <summary>
    ///     True when <paramref name="zoneId" /> is one of the two "open PvP" maps (324, or 335/FFAMAPNUM) where
    ///     <see cref="CombatResolver.ResolveEnemyTribeAttack" />'s same-tribe/allied-tribe rejection is skipped
    ///     entirely -- the value to pass as that method's <c>sameTribeAttackExempt</c> argument for an attack
    ///     occurring in this zone. Zone 324's narrative purpose beyond sharing this exemption with 335 is not
    ///     established by the citations this method is built from; treat it only as "a second zone with
    ///     byte-identical exemption behavior," not necessarily also a full FFA map in every other sense 335 is.
    /// </summary>
    public static bool IsSameTribeAttackExempt(short zoneId)
    {
        return SameTribeAttackExemptZoneIds.Contains(zoneId);
    }

    /// <summary>
    ///     True when <paramref name="zoneId" /> enforces the newbie-protection level gate -- the value to pass as
    ///     <see cref="CombatResolver.ResolveEnemyTribeAttack" />'s <c>newbieProtectionZone</c> argument for an
    ///     attack occurring in this zone.
    /// </summary>
    public static bool IsNewbieProtectionZone(short zoneId)
    {
        return NewbieProtectionZoneIds.Contains(zoneId);
    }
}
