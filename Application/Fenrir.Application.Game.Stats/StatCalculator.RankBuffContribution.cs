using System.Collections.Frozen;
using Fenrir.Application.Game.Stats.Context;

namespace Fenrir.Application.Game.Stats;

public static partial class StatCalculator
{
    // aRankBuffType (1-7) -> (boosted stat, flat magnitude). +1000 for tiers 1-6; tier 7 is +500.
    private static readonly FrozenDictionary<int, RankBuffEffect> RankBuffEffectsByTier =
        new Dictionary<int, RankBuffEffect>
        {
            [1] = new(RankBuffStat.DefensePower, 1000),
            [2] = new(RankBuffStat.ElementDefensePower, 1000),
            [3] = new(RankBuffStat.ElementAttackPower, 1000),
            [4] = new(RankBuffStat.AttackBlock, 1000),
            [5] = new(RankBuffStat.AttackSuccess, 1000),
            [6] = new(RankBuffStat.MaxLife, 1000),
            [7] = new(RankBuffStat.AttackPower, 500)
        }.ToFrozenDictionary();

    // Zones where the rank-buff produces no observable stat change for any tier -- see the block comment above.
    private static readonly FrozenSet<short> RankBuffSuppressedZones = ((short[])[124, 335]).ToFrozenSet();

    /// <summary>
    ///     The flat rank-buff contribution to <paramref name="stat" /> for the character's currently active tier
    ///     (<see cref="ZoneContext.RankBuffType" />). Returns 0 when no tier is active, the active tier targets a
    ///     different stat, or the character is in a suppressed zone (124 / 335). Pure integer arithmetic -- the
    ///     magnitudes are exact whole numbers, no truncation involved.
    /// </summary>
    private static int RankBuffBonusFor(RankBuffStat stat, ZoneContext zone)
    {
        if (zone.RankBuffType == 0)
            return 0;
        if (RankBuffSuppressedZones.Contains(zone.ZoneNumber))
            return 0;

        return RankBuffEffectsByTier.TryGetValue(zone.RankBuffType, out var effect) && effect.Stat == stat
            ? effect.Magnitude
            : 0;
    }

    // ---- Per-stat accessors: one per affected getter. See the wiringManifest for each call-site insertion. ----

    /// <summary>BASE layer: folded into the recompute-on-event max-life cache (ComputeMaxLife). Tier 6, +1000.</summary>
    public static int RankBuffMaxLifeBonus(ZoneContext zone)
    {
        return RankBuffBonusFor(RankBuffStat.MaxLife, zone);
    }

    /// <summary>EFFECTIVE layer: applied on top of the cached base per resolution (ComputeEffectiveStats). Tier 1, +1000.</summary>
    public static int RankBuffDefensePowerBonus(ZoneContext zone)
    {
        return RankBuffBonusFor(RankBuffStat.DefensePower, zone);
    }

    /// <summary>EFFECTIVE layer (ComputeEffectiveStats). Tier 2, +1000.</summary>
    public static int RankBuffElementDefensePowerBonus(ZoneContext zone)
    {
        return RankBuffBonusFor(RankBuffStat.ElementDefensePower, zone);
    }

    /// <summary>EFFECTIVE layer (ComputeEffectiveStats). Tier 3, +1000.</summary>
    public static int RankBuffElementAttackPowerBonus(ZoneContext zone)
    {
        return RankBuffBonusFor(RankBuffStat.ElementAttackPower, zone);
    }

    /// <summary>EFFECTIVE layer (ComputeEffectiveStats). Tier 4, +1000.</summary>
    public static int RankBuffAttackBlockBonus(ZoneContext zone)
    {
        return RankBuffBonusFor(RankBuffStat.AttackBlock, zone);
    }

    /// <summary>EFFECTIVE layer (ComputeEffectiveStats). Tier 5, +1000.</summary>
    public static int RankBuffAttackSuccessBonus(ZoneContext zone)
    {
        return RankBuffBonusFor(RankBuffStat.AttackSuccess, zone);
    }

    /// <summary>EFFECTIVE layer (ComputeEffectiveStats). Tier 7, +500 (the sole non-1000 magnitude).</summary>
    public static int RankBuffAttackPowerBonus(ZoneContext zone)
    {
        return RankBuffBonusFor(RankBuffStat.AttackPower, zone);
    }
    // ======================== WORKSTREAM B7 (rank-buff per-tier stat contribution) ========================
    //
    // A character's active rank-buff tier (aRankBuffType, 1-7; 0 = none -- ZoneContext.RankBuffType) boosts
    // EXACTLY ONE stat by a flat amount at every stat computation. The tier->stat->magnitude mapping was
    // independently re-verified against Server/ (whole-file scan of aRankBuffType in MyFactor.cpp returns
    // exactly seven consumption sites and no others -- the mapping is complete):
    //   tier 1 -> Defense power        +1000   MyFactor.cpp:4118-4120 (GetDefensePower)
    //   tier 2 -> Element-defense power +1000   MyFactor.cpp:4364-4366 (GetElementDefensePower)
    //   tier 3 -> Element-attack power  +1000   MyFactor.cpp:4313-4315 (GetElementAttackPower)
    //   tier 4 -> Attack-block / dodge  +1000   MyFactor.cpp:4246-4248 (GetAttackBlock)
    //   tier 5 -> Attack-success / hit  +1000   MyFactor.cpp:4197-4199 (GetAttackSuccess)
    //   tier 6 -> Maximum life (HP)     +1000   MyFactor.cpp:2130-2132 (GetBaseMaxLife)   <- BASE cache
    //   tier 7 -> Attack power / damage +500    MyFactor.cpp:4037-4039 (GetAttackPower)    <- the +500 exception
    //
    // LAYER split (see ZoneContext's own per-field doc, and this class's B1 remarks):
    //   * Tier 6 (HP) is folded into the BASE max-life cache -- its legacy block lives inside GetBaseMaxLife.
    //     Wired at the tail of ComputeMaxLife (StatCalculator.Life.cs). See wiringManifest.
    //   * Tiers 1-5,7 are applied by the per-read RUNTIME getters on top of the cached base -- NOT written into
    //     any base cache. In Fenrir the runtime-getter layer is ComputeEffectiveStats (StatCalculator.cs).
    //     Wired there, one flat additive term per stat. See wiringManifest.
    // The observable result is identical either way (the selection handler recomputes the base right after
    // storing the tier), but the split must be preserved: a base input needs a recompute event, an effective
    // input is read live.
    //
    // ZONE SUPPRESSION -- the rank-buff yields NO observable stat change for ANY tier in two zones:
    //   * 124 -- a hard-coded stat-neutral zone; each of the seven per-tier blocks is guarded "zone != 124"
    //            (MyFactor.cpp:4037/4118/2130/... per-site guards). Purpose not established in the source;
    //            treated as an opaque stat-neutral zone, nothing more inferred.
    //   * 335 -- FFAMAPNUM; the FFA "equal real stats" early return discards (GetBaseMaxLife, tail,
    //            MyFactor.cpp:2216-2223) or precedes (the runtime getters, e.g. GetAttackPower top,
    //            MyFactor.cpp:4009-4016) the rank-buff add, so no tier is ever observable there.
    //            (FFAMAPNUM=335: DEFINE.h:99, MyFactor.cpp:58-59; gate FFA_CheckEqualStatsZone MyFactor.cpp:64-69.)
    // NOTE: Fenrir's separate FfaEqualStatsOverride (Domain.Combat) overwrites the effective COMBAT stats in
    // zone 335 anyway, but deliberately leaves MaxLife untouched -- so suppressing 335 HERE is what actually
    // keeps the tier-6 HP bonus from leaking in zone 335. Handling both 124 and 335 at the contribution level
    // makes every tier self-consistently suppressed regardless of which getter consumes it.

    /// <summary>The single stat a given rank-buff tier boosts (each tier 1-7 targets exactly one stat).</summary>
    private enum RankBuffStat
    {
        None = 0,
        DefensePower = 1, // tier 1 (EFFECTIVE)
        ElementDefensePower = 2, // tier 2 (EFFECTIVE)
        ElementAttackPower = 3, // tier 3 (EFFECTIVE)
        AttackBlock = 4, // tier 4 (EFFECTIVE)
        AttackSuccess = 5, // tier 5 (EFFECTIVE)
        MaxLife = 6, // tier 6 (BASE cache)
        AttackPower = 7 // tier 7 (EFFECTIVE) -- the +500 exception
    }

    /// <summary>A rank-buff tier's target stat and its flat, unscaled magnitude.</summary>
    private readonly record struct RankBuffEffect(RankBuffStat Stat, int Magnitude);
}
