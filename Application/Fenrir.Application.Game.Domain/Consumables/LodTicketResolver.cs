using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Domain.Consumables;

/// <summary>
///     Item 1434, the "Life or Death" (LOD) round-extension ticket -- one of the two catalog ids special-cased
///     directly in <c>CZ_USE_INVENTORY_ITEM_SEND</c>'s shared prologue handler (the other being the rare
///     mount-box id), bypassing the normal per-item-sort dispatch entirely. No I/O, no Zone dependency: the
///     caller supplies the character's current level/rebirth/banked-round state and slot quantity, already
///     read off <c>PlayerRuntimeState</c>.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:2032-2117 (item 1434 direct handler -- level-cap and
///     rebirth-count preconditions, slot-quantity precondition, single-unit decrement-and-clear consumption
///     distinct from both the bulk-aware and plain single-unit helpers) ; Server/Header/function.h:132-140
///     (<c>CheckOverMaximum</c>, the wide-safe ceiling check this counter uses) ; Server/Header/Protocol/
///     DEFINE.h:365 (2,000,000,000 shared ceiling).
/// </remarks>
public static class LodTicketResolver
{
    public enum Outcome
    {
        Success,

        /// <summary>Base level not at the level cap, or rebirth counter &lt; 1 -- clean failure.</summary>
        PreconditionFailed,

        /// <summary>Slot's stacked quantity is &lt; 1 -- clean failure (should not be reachable if the slot resolved at all).</summary>
        InsufficientQuantity,

        /// <summary>Adding one more round would exceed the 2,000,000,000 ceiling -- clean failure, counter untouched.</summary>
        WouldExceedCeiling
    }

    public static Result Resolve(short characterLevel, int rebirthCount, int slotQuantity, int currentLodRounds)
    {
        if (characterLevel < LevelProgressionCalculator.MaxLevel || rebirthCount < 1)
            return new Result(Outcome.PreconditionFailed, currentLodRounds);

        if (slotQuantity < 1)
            return new Result(Outcome.InsufficientQuantity, currentLodRounds);

        var added = BankedCounterMath.AddWideSafe(currentLodRounds, 1);
        return added.Succeeded
            ? new Result(Outcome.Success, added.NewValue)
            : new Result(Outcome.WouldExceedCeiling, currentLodRounds);
    }

    public readonly record struct Result(Outcome Outcome, int NewLodRounds)
    {
        public bool Succeeded => Outcome == Outcome.Success;
    }
}
