using System.Collections.Frozen;
using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Stats;

public static partial class StatCalculator
{
    /// <summary>
    ///     Zone(s) where the tribe-role term is suppressed entirely for all three stats. The contract cites
    ///     exactly one: 124 (<c>MyFactor.cpp:4055</c>, <c>:4131</c>, <c>:4450</c>). Held as a set for parity with
    ///     the sibling suppression tables and to make an added future gate a one-line change.
    /// </summary>
    private static readonly FrozenSet<short> TribeRoleSuppressedZones = ((short[])[124]).ToFrozenSet();

    /// <summary>
    ///     Shared DMG/DEF term: +200 for a tribe master, +100 for a sub-master, nothing otherwise, and nothing at
    ///     all in a suppressed zone. Pure integer arithmetic -- the magnitudes are exact whole numbers.
    ///     <c>GetAttackPower</c> (<c>MyFactor.cpp:4058-4061</c>) and <c>GetDefensePower</c>
    ///     (<c>MyFactor.cpp:4134-4137</c>) use identical values.
    /// </summary>
    private static int TribeRoleAttackDefenseBonus(ZoneContext zone)
    {
        if (TribeRoleSuppressedZones.Contains(zone.ZoneNumber))
            return 0;

        return (TribeRoleKind)zone.TribeRole switch
        {
            TribeRoleKind.Master => 200,
            TribeRoleKind.SubMaster => 100,
            _ => 0
        };
    }

    // ---- Per-stat accessors: one per affected runtime getter. See the wiringManifest for each call-site. ----

    /// <summary>
    ///     EFFECTIVE layer (folded into <see cref="ComputeEffectiveStats" />'s attack-power block, flat/unscaled):
    ///     +200 master, +100 sub-master, else 0; 0 in zone 124. Legacy <c>GetAttackPower</c> /
    ///     <c>MyFactor.cpp:4055-4062</c>.
    /// </summary>
    public static int TribeRoleAttackPowerBonus(ZoneContext zone)
    {
        return TribeRoleAttackDefenseBonus(zone);
    }

    /// <summary>
    ///     EFFECTIVE layer (folded into <see cref="ComputeEffectiveStats" />'s defense-power block, flat/unscaled):
    ///     +200 master, +100 sub-master, else 0; 0 in zone 124. Legacy <c>GetDefensePower</c> /
    ///     <c>MyFactor.cpp:4131-4138</c> -- identical shape and values to attack power.
    /// </summary>
    public static int TribeRoleDefensePowerBonus(ZoneContext zone)
    {
        return TribeRoleAttackDefenseBonus(zone);
    }

    /// <summary>
    ///     EFFECTIVE layer (folded into <see cref="ComputeEffectiveStats" />'s critical block, flat/unscaled):
    ///     +2 for a tribe master ONLY; a sub-master (and everyone else) gets nothing; 0 in zone 124. Legacy
    ///     <c>GetCritical</c> / <c>MyFactor.cpp:4450-4452</c> -- the master/sub-master asymmetry versus DMG/DEF.
    /// </summary>
    public static int TribeRoleCriticalBonus(ZoneContext zone)
    {
        if (TribeRoleSuppressedZones.Contains(zone.ZoneNumber))
            return 0;

        return (TribeRoleKind)zone.TribeRole == TribeRoleKind.Master ? 2 : 0;
    }
    // ===================== WORKSTREAM B7 (tribe master/sub-master role combat-stat bonus) =====================
    //
    // A character's resolved tribe role (aTribe roster role -- ReturnTribeRole, exposed here as
    // ZoneContext.TribeRole: 0 regular / 1 master / 2 sub-master / 3 council) adds a flat, LATE term to three
    // derived combat stats. The three consumption sites in the legacy runtime getters are:
    //   DMG  (GetAttackPower)   MyFactor.cpp:4055-4062  -> +200 master (role 1), +100 sub-master (role 2), else 0
    //   DEF  (GetDefensePower)  MyFactor.cpp:4131-4138  -> +200 master (role 1), +100 sub-master (role 2), else 0
    //   CRIT (GetCritical)      MyFactor.cpp:4450-4452  -> +2  master (role 1) ONLY; sub-master gets nothing
    // The master/sub-master asymmetry on CRIT (role 2 gets 0) is the one non-uniformity across the three stats.
    //
    // LAYER: all three are EFFECTIVE-layer terms. In legacy they are added inside the per-read runtime getters
    // (GetAttackPower/GetDefensePower/GetCritical), AFTER every base/buff-percent/pet/socket/rank/animal/druk
    // term, as a flat addition that is never scaled by any multiplier (contract "flat, late addition"). In
    // Fenrir the runtime-getter layer is ComputeEffectiveStats (StatCalculator.cs) -- NOT the base Compute*
    // getters, which map to legacy GetBase* and feed the recompute-on-event cache. See the wiringManifest: each
    // term is appended at the tail of its stat's ComputeEffectiveStats block (after ApplyBuffPercent /
    // ApplyPetDoubleRule / CapeIuBonus), so it stays flat and unscaled.
    //
    // ZONE SUPPRESSION: zone 124 is a hard gate that suppresses the ENTIRE tribe-role term on all three stats
    // (MyFactor.cpp:4055, :4131, :4450); every other zone lets it apply. This is the ONLY zone gate the
    // contract cites for tribe-role. (Unlike the co-located rank-buff mechanic -- StatCalculator.RankBuffContribution
    // -- which additionally suppresses 335/FFA at the contribution level, the tribe-role contract does not cite a
    // 335 gate. All three tribe-role stats are combat stats that Fenrir's FfaEqualStatsOverride (Domain.Combat)
    // clobbers in zone 335 anyway, so no contribution-level 335 handling is needed here to match legacy -- and
    // inventing one would exceed the contract's citations.)
    //
    // ROLE RESOLUTION IS UPSTREAM, NOT HERE: the role code this reads (ZoneContext.TribeRole, sourced from
    // PlayerRuntimeState.TribeRole) is already resolved by the roster decode elsewhere (world entry /
    // appoint-dismiss / tribe-vote election). The contract's decode rules -- exact case-sensitive name match
    // against the character's OWN tribe row, master slot checked first then up to 12 sub-master slots, out-of-range
    // tribe -> role 0, and the "vote/council" tier (role 3) never earning a combat bonus -- describe how that
    // upstream value is produced; this stats-contribution slice only consumes the resolved code. Role 3 and any
    // value that is neither 1 nor 2 therefore yield no bonus here, which is exactly what the decode's {0,1,2}
    // combat-path domain requires.

    /// <summary>
    ///     A character's resolved tribe role, as carried by <see cref="ZoneContext.TribeRole" /> /
    ///     <c>PlayerRuntimeState.TribeRole</c> (legacy <c>ReturnTribeRole</c>). Only <see cref="Master" /> and
    ///     <see cref="SubMaster" /> earn a combat-stat bonus; <see cref="Council" /> (the elected vote candidate)
    ///     and <see cref="None" /> earn nothing -- matching the combat-path decode's {0,1,2} domain, where the
    ///     vote tier is produced by a separate, non-combat decoder only.
    /// </summary>
    private enum TribeRoleKind : byte
    {
        None = 0, // regular member -- no bonus
        Master = 1, // +200 DMG/DEF, +2 CRIT
        SubMaster = 2, // +100 DMG/DEF, no CRIT
        Council = 3 // elected vote candidate -- NO combat bonus (never reaches the combat-path decode)
    }
}
