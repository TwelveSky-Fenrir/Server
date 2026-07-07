namespace Fenrir.Application.Game.Domain.Consumables;

/// <summary>
///     Generic cap/bulk math shared by every stat-boost potion/elixir id (HP, MP, STR, DEX, and the two
///     Elemental sub-stats; base and "g12" high-tier variants). No I/O, no Zone dependency.
/// </summary>
/// <remarks>
///     Wired to the 28 production catalog ids (item-usage-consumables finding, Critical) by
///     <c>Fenrir.Application.Game.Services.ZoneLifecycle.UseInventoryItemService.ResolveStatPotionSpec</c> --
///     see that method's own remarks for the full id-to-(stat,tier) table and its provenance (the
///     behavior contract's cited tAddTime/tAddTime2 pairs for the Elemental ids, the finding's own
///     statement that 801-806 are the g12 tier, cross-referenced against Fenrir's own already-imported
///     world.Items catalog text for the remaining ordinary-tier ids). The Elemental counter's
///     divide/modulo-1000 packed-dual-value convention is modeled by the sibling
///     <see cref="ElementalPotionPacking" /> type, one layer above this one -- this type only ever operates
///     on an already-unpacked sub-value, kind-agnostic by design.
/// </remarks>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:2879-2947 (id-to-stat/tier mapping, per-id fixed add
///     amounts) ; :2948-3156 (per-stat cap check, headroom-based bulk-count coercion, g12-tier
///     preconditions and bulk-aware add) ; :3157-3211 (stat/HP-MP recompute, stack consumption, double
///     self+nearby broadcast) ; Server/Header/Protocol/DEFINE.h:361-362 (200 ordinary-tier cap, 400
///     g12-tier cap) ; Server/Header/Protocol/DEFINE.h:605 (12, MAX_LIMIT_HIGH_LEVEL_NUM -- the g12 gate is
///     wAvatar.aLevel2, NOT wAvatar.aRebirthNum: the two are declared as distinct fields
///     (Server/Header/Protocol/STRUCT.h:471,841) and used as independent, alternative conditions elsewhere
///     (Server/Header/S18_MyZoneInfo.cpp:2211) -- an earlier revision of this type mistakenly named its
///     g12 threshold parameter after "rebirth count"; fixed here to <see cref="RequiredLevel2" />/
///     <c>level2</c> throughout).
/// </remarks>
public static class StatPotionResolver
{
    public enum Outcome
    {
        Success,

        /// <summary>
        ///     The banked counter is already exactly at its tier cap -- a distinct result code, not the plain failure used
        ///     elsewhere.
        /// </summary>
        AlreadyMaxed,

        /// <summary>
        ///     g12 tier only: banked count not yet at the ordinary cap, still at/above the g12 cap, or the Level2 threshold
        ///     not met.
        /// </summary>
        PreconditionFailed
    }

    public const int OrdinaryTierCap = 200;
    public const int G12TierCap = 400;

    /// <summary>MAX_LIMIT_HIGH_LEVEL_NUM (DEFINE.h:605) -- the g12 tier's own level-progression gate, wAvatar.aLevel2.</summary>
    public const int RequiredLevel2 = 12;

    /// <summary>
    ///     Ordinary tier ("1x"/"10x" ids): bulk-aware. If the requested bulk count would overshoot
    ///     <see cref="OrdinaryTierCap" />, it is silently reduced to whatever still fits rather than the whole
    ///     request being rejected -- only a truly-zero headroom (already at cap) yields
    ///     <see cref="Outcome.AlreadyMaxed" />.
    /// </summary>
    public static OrdinaryResult ResolveOrdinary(int currentCount, int perUnitAmount, int requestedBulkCount)
    {
        var coercedCount =
            BankedCounterMath.CoerceBulkToHeadroom(currentCount, OrdinaryTierCap, perUnitAmount, requestedBulkCount);

        if (coercedCount <= 0)
            return new OrdinaryResult(Outcome.AlreadyMaxed, currentCount, 0);

        return new OrdinaryResult(Outcome.Success, currentCount + perUnitAmount * coercedCount, coercedCount);
    }

    /// <summary>
    ///     g12 high-tier ids: gated on the banked count already being at-or-above <see cref="OrdinaryTierCap" />,
    ///     still below <see cref="G12TierCap" />, and <paramref name="level2" /> (wAvatar.aLevel2) being
    ///     at-or-above <see cref="RequiredLevel2" />. Bulk-aware like <see cref="ResolveOrdinary" /> --
    ///     <paramref name="requestedBulkCount" /> is clamped down to whatever headroom remains under
    ///     <see cref="G12TierCap" /> (never rejected for being merely too large, only for failing the three
    ///     preconditions above), and each consumed unit adds exactly 1 to the counter (no separate
    ///     per-unit-amount multiplier the way the ordinary tier's 10x ids have -- no ten-unit-stack g12 id
    ///     exists in the 28-id set this resolver serves).
    /// </summary>
    public static G12Result ResolveG12(int currentCount, int level2, int requestedBulkCount)
    {
        if (currentCount < OrdinaryTierCap || currentCount >= G12TierCap || level2 < RequiredLevel2)
            return new G12Result(Outcome.PreconditionFailed, currentCount, 0);

        // Guaranteed >= 1 by the "< G12TierCap" precondition above -- no separate zero-headroom rejection
        // branch is needed (or legacy-accurate) here, unlike the ordinary tier's AlreadyMaxed case.
        var headroom = G12TierCap - currentCount;
        var coercedCount = Math.Min(Math.Max(requestedBulkCount, 1), headroom);

        return new G12Result(Outcome.Success, currentCount + coercedCount, coercedCount);
    }

    public readonly record struct OrdinaryResult(Outcome Outcome, int NewCount, int UnitsConsumed)
    {
        public bool Succeeded => Outcome == Outcome.Success;
    }

    public readonly record struct G12Result(Outcome Outcome, int NewCount, int UnitsConsumed)
    {
        public bool Succeeded => Outcome == Outcome.Success;
    }
}
