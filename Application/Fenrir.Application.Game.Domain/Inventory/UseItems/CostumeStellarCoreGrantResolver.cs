using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

/// <summary>
///     Pure "already worn / first free slot" decision shared by op23's costume and stellar-core wardrobe grant
///     (workstream C9-costume-stellar-whitelist) -- structurally identical for both ten-slot arrays per the
///     originating contract's own Side effects section ("Stellar-core match... is structurally identical").
///     Not the same mechanism as <see cref="Costumes.CostumeStateResolver" />/
///     <see cref="StellarCores.StellarCoreStateResolver" /> (op90/op153's own select/equip/remove/
///     return-to-inventory sort dispatch) -- this resolver only decides whether a NEW item may be granted INTO
///     the wardrobe in the first place.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork03.cpp:2493-2514 (costume: duplicate-id check, free-slot search,
///     disconnect-on-full) and :2532-2553 (stellar core, identical shape).
/// </remarks>
public static class CostumeStellarCoreGrantResolver
{
    public enum Outcome
    {
        /// <summary>The item's id already occupies a slot in this wardrobe -- reject, no side effects.</summary>
        AlreadyWorn,

        /// <summary>Every slot is occupied by something else -- the caller must disconnect, no side effects.</summary>
        NoFreeSlot,

        /// <summary><see cref="Result.SlotIndex" /> is the first free slot to grant into.</summary>
        Success
    }

    public readonly record struct Result(Outcome Outcome, int SlotIndex = -1);

    /// <summary>
    ///     Scans <paramref name="wardrobe" /> for <paramref name="itemId" /> (duplicate check) then, if absent,
    ///     for the first zero (free) slot. <paramref name="wardrobe" /> is expected to be a real, populated
    ///     10-slot array (<see cref="Costumes.CostumeStateResolver.SlotCount" /> /
    ///     <see cref="StellarCores.StellarCoreStateResolver.SlotCount" />) -- an empty/default array is treated
    ///     as "no slots at all," resolving to <see cref="Outcome.NoFreeSlot" /> defensively rather than
    ///     throwing.
    /// </summary>
    public static Result Resolve(ImmutableArray<int> wardrobe, int itemId)
    {
        if (wardrobe.IsDefaultOrEmpty)
            return new Result(Outcome.NoFreeSlot);

        foreach (var slotItemId in wardrobe)
            if (slotItemId == itemId)
                return new Result(Outcome.AlreadyWorn);

        for (var slot = 0; slot < wardrobe.Length; slot++)
            if (wardrobe[slot] == 0)
                return new Result(Outcome.Success, slot);

        return new Result(Outcome.NoFreeSlot);
    }
}
