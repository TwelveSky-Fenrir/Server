namespace Fenrir.Application.Game.Domain.Consumables;

/// <summary>
///     Pet EXP boost pill (world.Items 1190/17035/8413, legacy <c>aPetExpX2Time</c>): bulk-aware, wide-safe
///     ceiling-checked charge onto <see cref="World.PlayerRuntimeState.PetExpX2Time" />, 180 units per unit
///     consumed. Same <c>CheckOverMaximum</c>-style ceiling check as the charm sub-group of
///     <see cref="ProtectionChargeResolver" />, kept as its own type since this counter is a duration timer,
///     not a protection/enhancement charge -- a different domain concept sharing only the underlying ceiling
///     math (<see cref="BankedCounterMath.AddWideSafe" />).
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:5440-5468 (case 1190/17035/8413, gated
///     <c>WUSE_ITEM_1190</c> -- unconditionally #define'd at Server/Header/use_inventory.h:46, live in every
///     build): the fixed 180-unit increase, bulk multiplication, and the overflow check/failure path ;
///     Server/Header/function.h:132-140 (<c>CheckOverMaximum</c>) ; Server/Header/Protocol/DEFINE.h:365
///     (2,000,000,000 shared ceiling). Item id 99402 (a fourth trigger id in the same legacy case block,
///     gated behind the non-production <c>ONLINE_FOR_DS</c> branch) is deliberately not modeled here -- see
///     <c>UseInventoryItemService</c>'s own remarks on its pet-exp-boost-pill branch.
/// </remarks>
public static class PetExpBoostPillResolver
{
    public enum ChargeOutcome
    {
        Success,

        /// <summary>Adding would exceed the shared 2,000,000,000 ceiling -- clean failure, counter untouched.</summary>
        WouldExceedCeiling
    }

    /// <summary>The fixed per-unit increase to <see cref="World.PlayerRuntimeState.PetExpX2Time" />.</summary>
    public const int PerUnitAmount = 180;

    /// <summary>
    ///     <paramref name="bulkUnitCount" /> is the already-coerced (<see cref="BulkUseCoercion.Coerce" />)
    ///     number of units being consumed in this request.
    /// </summary>
    public static ChargeResult ResolveCharge(int currentPetExpX2Time, int bulkUnitCount)
    {
        var totalAmount = (long)PerUnitAmount * bulkUnitCount;
        var added = BankedCounterMath.AddWideSafe(currentPetExpX2Time, totalAmount);

        return added.Succeeded
            ? new ChargeResult(ChargeOutcome.Success, added.NewValue)
            : new ChargeResult(ChargeOutcome.WouldExceedCeiling, currentPetExpX2Time);
    }

    public readonly record struct ChargeResult(ChargeOutcome Outcome, int NewCounterValue)
    {
        public bool Succeeded => Outcome == ChargeOutcome.Success;
    }
}
