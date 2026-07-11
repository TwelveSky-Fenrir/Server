using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Inventory.UseItems;

public static class CostumeStellarCoreGrantResolver
{
    public enum Outcome
    {

                AlreadyWorn,

                NoFreeSlot,

                Success
    }

    public readonly record struct Result(Outcome Outcome, int SlotIndex = -1);

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
