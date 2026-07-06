namespace Fenrir.Application.Game.Domain.Consumables;

/// <summary>
///     Shared math for every "banked protection/enhancement charge" counter family (Preserve/Protection/
///     Guardian Charm, Absolute Craft Ticket, CP Prot Charm, Lucky Enchant/Combine/Upgrade/Drop Scroll). No
///     I/O, no Zone dependency -- the caller resolves which counter/per-id charge amount applies (see
///     <c>UseInventoryItemService</c>'s own id-to-amount tables) and hands the already-resolved shape here.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:2677-2701 (Preserve Charm) ; :3728-3764 (Protection Charm,
///     five-charge variant) ; :3766-3792 (Guardian Charm) ; :3794-3818 (Lucky Enchant Scroll) ; :3820-3845
///     (Absolute Craft Ticket) ; :4185-4247 (Lucky Combine/Upgrade/Drop Scrolls) ; :4959-5002 (CP Prot Charm,
///     halo-rank gate, three-charge variant, build-flag-gated id 99405) ; Server/Header/function.h:132-140
///     (<c>CheckOverMaximum</c>, the charm sub-group's wide-safe ceiling check) ; Server/ts25zone/
///     S04_MyWork03.cpp:106-117 (<c>wCheckAdd</c>/<c>wCheckAdd2</c>, the scroll sub-group's narrower
///     32-bit-addition-then-compare style -- a documented, deliberately-preserved inconsistency, not
///     normalized to one style here).
/// </remarks>
public static class ProtectionChargeResolver
{
    /// <summary>CP Prot Charm's own precondition: a halo rank at or above this rejects the charge outright, regardless of ceiling headroom.</summary>
    public const int HaloRankGateThreshold = 96;

    public enum ChargeOutcome
    {
        Success,

        /// <summary>Adding would exceed the shared 2,000,000,000 ceiling -- clean failure, counter untouched.</summary>
        WouldExceedCeiling,

        /// <summary>CP Prot Charm only: halo rank already &gt;= <see cref="HaloRankGateThreshold" />.</summary>
        HaloRankTooHigh
    }

    /// <summary>
    ///     Charm sub-group (Preserve/Protection/Guardian/Absolute Craft/CP Prot Charm): bulk-aware, wide-safe
    ///     ceiling check via <see cref="BankedCounterMath.AddWideSafe" />. <paramref name="bulkUnitCount" /> is
    ///     the already-coerced (<see cref="BulkUseCoercion.Coerce" />) number of units being consumed in this
    ///     request.
    /// </summary>
    public static ChargeResult ResolveCharmCharge(int currentCounter, int perUnitAmount, int bulkUnitCount)
    {
        var totalAmount = (long)perUnitAmount * bulkUnitCount;
        var added = BankedCounterMath.AddWideSafe(currentCounter, totalAmount);

        return added.Succeeded
            ? new ChargeResult(ChargeOutcome.Success, added.NewValue, bulkUnitCount)
            : new ChargeResult(ChargeOutcome.WouldExceedCeiling, currentCounter, 0);
    }

    /// <summary>
    ///     CP Prot Charm's own variant of <see cref="ResolveCharmCharge" />: the halo-rank gate is checked
    ///     first and rejects independently of ceiling headroom -- this precondition does not generalize to any
    ///     other charm id in this family.
    /// </summary>
    public static ChargeResult ResolveCpProtCharmCharge(int currentCounter, int perUnitAmount, int bulkUnitCount,
        int haloRank)
    {
        return haloRank >= HaloRankGateThreshold
            ? new ChargeResult(ChargeOutcome.HaloRankTooHigh, currentCounter, 0)
            : ResolveCharmCharge(currentCounter, perUnitAmount, bulkUnitCount);
    }

    /// <summary>
    ///     Scroll sub-group (Lucky Enchant/Combine/Upgrade/Drop): single-unit only, no bulk use at all -- every
    ///     use consumes exactly one unit via the plain single-unit consumption helper, and the ceiling is
    ///     checked with the narrower <see cref="BankedCounterMath.AddNarrow" /> style instead.
    /// </summary>
    public static ChargeResult ResolveScrollCharge(int currentCounter, int fixedAmount)
    {
        var added = BankedCounterMath.AddNarrow(currentCounter, fixedAmount);

        return added.Succeeded
            ? new ChargeResult(ChargeOutcome.Success, added.NewValue, 1)
            : new ChargeResult(ChargeOutcome.WouldExceedCeiling, currentCounter, 0);
    }

    public readonly record struct ChargeResult(ChargeOutcome Outcome, int NewCounterValue, int UnitsConsumed)
    {
        public bool Succeeded => Outcome == ChargeOutcome.Success;
    }
}
