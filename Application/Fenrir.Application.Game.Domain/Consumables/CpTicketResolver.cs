using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Consumables;

/// <summary>
///     The op23 (<c>CZ_USE_INVENTORY_ITEM_SEND</c>) CP Ticket family (world.Items 691-694, 1444, 1447-1449,
///     1499, 8431-8434): a bulk-aware currency credit onto <see cref="World.PlayerRuntimeState.ContributionPoints" />
///     (aKillOtherTribe). No I/O, no Zone dependency -- reuses <see cref="BulkUseCoercion.Coerce" /> for the
///     shared "how many at once" clamp (the originating contract's own <c>GetBulkStackUseCount</c> helper,
///     confirmed to floor its own result at 1 -- see this type's own remarks) and
///     <see cref="BankedCounterMath.AddWideSafe" /> for the overflow guard, rather than duplicating either.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:5267-5367 (CP Ticket family: the per-item amount table, the
///     bulk-use-count computation via the shared helper, the overflow guard, the direct currency credit, and
///     the single bulk-count consumption -- personally re-opened and confirmed in the originating
///     C9-tickets-tower contract, including the exact amounts: 691→5, 692→10, 693→15, 694→20, 1444→500,
///     {1447,8433}→1000, {1448,8432}→500, {1449,8431}→100, {1499,8434}→5000) ; :629-669
///     (<c>GetBulkStackUseCount</c>, the shared bulk-use-count helper this resolver's caller applies via
///     <see cref="BulkUseCoercion.Coerce" /> -- confirmed by the originating contract to floor its own result
///     at 1 in two separate places, meaning a bulk-count-below-1 rejection can never actually trigger: no such
///     rejection path is modeled here, matching that contract's own Edge cases note).
/// </remarks>
public static class CpTicketResolver
{
    public enum Outcome
    {
        Success,

        /// <summary>The addressed item id is not a member of this family, or the slot's stacked quantity is &lt; 1.</summary>
        InsufficientQuantity,

        /// <summary>The resulting total currency grant would exceed the shared 2,000,000,000 ceiling -- clean failure, untouched.</summary>
        WouldExceedCeiling
    }

    private static readonly FrozenDictionary<int, int> PerUnitAmounts = new Dictionary<int, int>
    {
        [691] = 5,
        [692] = 10,
        [693] = 15,
        [694] = 20,
        [1444] = 500,
        [1447] = 1000,
        [8433] = 1000,
        [1448] = 500,
        [8432] = 500,
        [1449] = 100,
        [8431] = 100,
        [1499] = 5000,
        [8434] = 5000
    }.ToFrozenDictionary();

    /// <summary>Every catalog id this family claims -- used by the registry wiring to route them all to one handler.</summary>
    public static IEnumerable<int> HandledItemIds => PerUnitAmounts.Keys;

    public static bool TryGetPerUnitAmount(int itemId, out int perUnitAmount)
    {
        return PerUnitAmounts.TryGetValue(itemId, out perUnitAmount);
    }

    /// <summary>
    ///     <paramref name="requestedValue" /> is the client-supplied wire "value" field (a bulk-use request, 0
    ///     on a plain click); <paramref name="stackQuantity" /> is the slot's own on-hand quantity.
    /// </summary>
    public static Result Resolve(int itemId, int requestedValue, int stackQuantity, int currentContributionPoints)
    {
        if (stackQuantity < 1 || !PerUnitAmounts.TryGetValue(itemId, out var perUnitAmount))
            return new Result(Outcome.InsufficientQuantity, 0, 0, currentContributionPoints);

        var bulkCount = BulkUseCoercion.Coerce(requestedValue, stackQuantity);
        var totalAmount = (long)perUnitAmount * bulkCount;
        var added = BankedCounterMath.AddWideSafe(currentContributionPoints, totalAmount);

        return added.Succeeded
            ? new Result(Outcome.Success, bulkCount, (int)totalAmount, added.NewValue)
            : new Result(Outcome.WouldExceedCeiling, 0, 0, currentContributionPoints);
    }

    public readonly record struct Result(
        Outcome Outcome,
        int UnitsConsumed,
        int CreditedAmount,
        int NewContributionPoints)
    {
        public bool Succeeded => Outcome == Outcome.Success;
    }
}
