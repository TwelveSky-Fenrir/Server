using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Tribes;

namespace Fenrir.Application.Game.Domain.Enchant;

/// <summary>
///     Pure resolver for the two costume target-type paths of CZ_IMPROVE_ITEM_SEND: costume enchant
///     (<c>USE_ENCHANT_COSTUME_V2</c>, DEFINE.h:122) and costume swap (<c>USE_SWAP_COSTUME_ENCHANT</c>,
///     DEFINE.h:123), both live under LNW33. No I/O, no Zone dependency. Reached by the op24 dispatch ahead of
///     the ordinary <see cref="EnchantResolver" /> band when the target is a costume; the coarse "this target
///     is a costume" classification is the routing service's job (same posture as
///     <see cref="StellarCoreResolver" />).
/// </summary>
/// <remarks>
///     Enchant cited to Server/ts25zone/S04_MyWork02.cpp:2619-2711, Server/Header/function.h:512-523 (flat
///     5,000,000 costume price, explicitly OUTSIDE the premium-discount guard so no
///     <see cref="Economy.PremiumPricing" /> call here). Swap cited to S04_MyWork02.cpp:2596-2612 and
///     Server/Header/function.h:2108 (<see cref="SwapMoneyCost" /> = 2,000,000,000).
///     <para>
///         Success probability for the enchant path comes from the same <c>GetHaloCostumeEnchantRate</c>
///         (function.h:2165-2214) already ported for tribe-halo enchant, so the flat success rate is reused
///         from <see cref="TribeHaloEnchantResolver.GetRates" /> rather than re-deriving it -- material
///         <see cref="GuaranteedSuccessMaterial" /> (724) short-circuits to a guaranteed success. The tribe
///         path's own <c>+2</c> call-site bonus is NOT applied here: it is a tribe-work-only addend
///         (S04_MyWork02.cpp:11128-11230), and the C12 contract does not cite any equivalent for the costume
///         call site -- flagged as an open question rather than assumed.
///     </para>
///     <para>
///         The costume failure/downgrade/protection sub-block is commented out in the production source, so a
///         failed costume enchant is a plain no-change (ZC result 8) with the material still consumed -- there
///         is deliberately no downgrade, no destroy, and no <c>ProtectForCostume</c> consumption on this path
///         (S04_MyWork02.cpp:2680-2711). Reaching exactly +96 sets <see cref="CostumeEnchantResult.ReachedCap" />
///         so the caller can emit the realm-wide notice (legacy center opcode 2101) -- collapsed to a log line
///         in Fenrir, same precedent as the wing/item cap notices (see <c>CenterRelayNoticeLog</c>).
///     </para>
///     Swap-compatibility beyond "both slots are costumes" is not further cited; the resolver exchanges the two
///     stored enchant values and reports the flat swap cost. The precise legacy swap-eligibility predicate is
///     flagged as an open question rather than invented.
/// </remarks>
public static class CostumeImproveResolver
{
    public enum CostumeEnchantOutcome
    {
        /// <summary>A real Quit() condition -- the caller must disconnect.</summary>
        Rejected,

        /// <summary>Enchant increased by 1 (ZC result 0).</summary>
        Success,

        /// <summary>Roll failed: no change at all, material still consumed (ZC result 8).</summary>
        NoChange
    }

    public enum CostumeSwapOutcome
    {
        Rejected,

        /// <summary>The two costumes' enchant values were exchanged (ZC result 999).</summary>
        Swapped
    }

    /// <summary>Costume enchant hard cap (Server/ts25zone/S04_MyWork02.cpp:2619-2623).</summary>
    public const int MaxCostumeImprove = 96;

    /// <summary>Ordinary costume enchant stone (rolls at the halo/costume rate).</summary>
    public const int OrdinaryMaterial = 8102;

    /// <summary>Guaranteed-success costume enchant material (Server/ts25zone/S04_MyWork02.cpp:2655-2656).</summary>
    public const int GuaranteedSuccessMaterial = 724;

    /// <summary>Flat costume enchant money cost (Server/Header/function.h:512-523) -- NO premium discount.</summary>
    public const int EnchantMoneyCost = 5_000_000;

    /// <summary>Flat costume enchant CP cost (Server/ts25zone/S04_MyWork02.cpp:2630-2645).</summary>
    public const int EnchantContributionPointCost = 25;

    /// <summary>Flat costume swap money cost (Server/Header/function.h:2108, MAX_SWAP_MONEY_COST).</summary>
    public const int SwapMoneyCost = 2_000_000_000;

    /// <summary>
    ///     Consumes one draw from <paramref name="random" /> unless the material forces a guaranteed success
    ///     (material 724), in which case no draw is taken -- matching the legacy's short-circuit.
    /// </summary>
    public static CostumeEnchantResult ResolveEnchant(int currentImprove, int materialItemId, IRandomSource random)
    {
        if (currentImprove >= MaxCostumeImprove)
            return CostumeEnchantResult.Rejected;

        if (materialItemId is not (OrdinaryMaterial or GuaranteedSuccessMaterial))
            return CostumeEnchantResult.Rejected;

        var newImprove = currentImprove + 1;

        bool success;
        if (materialItemId == GuaranteedSuccessMaterial)
        {
            success = true;
        }
        else
        {
            // Reuse the ported GetHaloCostumeEnchantRate flat success rate -- see this type's own remarks.
            var (successRate, _) = TribeHaloEnchantResolver.GetRates(currentImprove);
            success = random.NextInt32(100) < successRate;
        }

        if (!success)
            return new CostumeEnchantResult(CostumeEnchantOutcome.NoChange, currentImprove,
                EnchantMoneyCost, EnchantContributionPointCost, false);

        return new CostumeEnchantResult(CostumeEnchantOutcome.Success, newImprove,
            EnchantMoneyCost, EnchantContributionPointCost, newImprove == MaxCostumeImprove);
    }

    /// <summary>
    ///     Exchanges the two costumes' stored enchant values. Preconditions on the two slots actually being
    ///     costumes (and any finer swap-compatibility rule) are the caller's job -- see this type's remarks.
    /// </summary>
    public static CostumeSwapResult ResolveSwap(int improveA, int improveB)
    {
        return new CostumeSwapResult(CostumeSwapOutcome.Swapped, SwapMoneyCost, improveB, improveA);
    }

    /// <summary>
    ///     <see cref="ReachedCap" /> is true only on the exact success that lands at +96, so the caller can emit
    ///     the cap notice once. <see cref="MaterialConsumed" /> is true on both Success and NoChange (the
    ///     material is always spent when the resolver did not reject).
    /// </summary>
    public readonly record struct CostumeEnchantResult(
        CostumeEnchantOutcome Outcome,
        int NewImprove,
        int MoneyCost,
        int ContributionPointCost,
        bool ReachedCap)
    {
        public static readonly CostumeEnchantResult Rejected =
            new(CostumeEnchantOutcome.Rejected, 0, 0, 0, false);

        public bool MaterialConsumed => Outcome is not CostumeEnchantOutcome.Rejected;
    }

    public readonly record struct CostumeSwapResult(
        CostumeSwapOutcome Outcome,
        int MoneyCost,
        int NewImproveA,
        int NewImproveB);
}
