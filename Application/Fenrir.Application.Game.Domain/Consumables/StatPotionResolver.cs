namespace Fenrir.Application.Game.Domain.Consumables;

/// <summary>
///     Generic cap/bulk math shared by every stat-boost potion/elixir id (HP, MP, STR, DEX, and the two
///     Elemental sub-stats; base and "g12" high-tier variants). No I/O, no Zone dependency.
/// </summary>
/// <remarks>
///     Not wired to any production catalog item id yet: the behavior contract this was built from gave the
///     rates/caps/tiers precisely but did not reproduce the exact id-to-(stat,tier,fixed-amount) table --
///     only grouped id ranges whose internal per-id tier assignment could not be reconstructed with
///     confidence (one candidate reading implies a doubled "Potion"+"Elixir" set per stat, which further
///     breaks a simple 1:1 id-to-tier mapping attempt). Wiring this to real item ids, and to the elemental
///     sub-stat "packed into a single stored value" detail (modeled here as a single shared counter, not a
///     bit-packed one, since no bit layout was specified), is a follow-up once the precise per-id table is
///     extracted from the cited source range. This type's cap/bulk/tier math is fully correct and tested
///     against fabricated ids in the meantime.
/// </remarks>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:2879-2947 (id-to-stat/tier mapping, per-id fixed add
///     amounts) ; :2948-3156 (per-stat cap check, headroom-based bulk-count coercion, g12-tier preconditions
///     and single-unit-only add) ; :3157-3211 (stat/HP-MP recompute, stack consumption, double self+nearby
///     broadcast) ; Server/Header/Protocol/DEFINE.h:361-362 (200 ordinary-tier cap, 400 g12-tier cap).
/// </remarks>
public static class StatPotionResolver
{
    public const int OrdinaryTierCap = 200;
    public const int G12TierCap = 400;

    public enum Outcome
    {
        Success,

        /// <summary>The banked counter is already exactly at its tier cap -- a distinct result code, not the plain failure used elsewhere.</summary>
        AlreadyMaxed,

        /// <summary>g12 tier only: banked count not yet at the ordinary cap, or rebirth/high-level threshold not met.</summary>
        PreconditionFailed
    }

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
    ///     g12 high-tier ids: single-unit only (bulk consumption does not apply to them at all), gated on the
    ///     banked count already being at-or-above <see cref="OrdinaryTierCap" /> and still below
    ///     <see cref="G12TierCap" />, plus a rebirth/high-level threshold whose numeric value was not specified
    ///     in the originating contract -- the caller must supply it (<paramref name="requiredRebirthLevel" />)
    ///     rather than this type guessing one.
    /// </summary>
    public static G12Result ResolveG12(int currentCount, int rebirthCount, int requiredRebirthLevel)
    {
        if (currentCount < OrdinaryTierCap || currentCount >= G12TierCap || rebirthCount < requiredRebirthLevel)
            return new G12Result(Outcome.PreconditionFailed, currentCount);

        return new G12Result(Outcome.Success, currentCount + 1);
    }

    public readonly record struct OrdinaryResult(Outcome Outcome, int NewCount, int UnitsConsumed)
    {
        public bool Succeeded => Outcome == Outcome.Success;
    }

    public readonly record struct G12Result(Outcome Outcome, int NewCount)
    {
        public bool Succeeded => Outcome == Outcome.Success;
    }
}
