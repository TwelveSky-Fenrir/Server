using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Domain.Game.GameData;

namespace Fenrir.Application.Game.Domain.Inventory;

public static class GroundItemPickupPolicy
{
    public enum Outcome
    {
        Money,

        Stacked,

        Placed,

        Rejected
    }

    public const float MaxPickupDistance = 100f;

    public const int MaxStackQuantity = 999;

    public static Result Resolve(ItemDefinition itemDefinition, GroundItemEntity groundItem, ItemStack? destinationSlot)
    {
        var sort = itemDefinition.Item.Sort;

        if (sort == 1)
            return new Result(Outcome.Money, null, groundItem.Quantity);

        var (enchant, combine, refine, socket) = ItemValueCodec.Decode(groundItem.Value);

        if (ContainerMatrix.IsStackableSort(sort))
        {
            if (destinationSlot is { } existingStack)
            {
                if (existingStack.ItemId != groundItem.ItemId)
                    return new Result(Outcome.Rejected, null, 0);

                var merged = existingStack.Quantity + groundItem.Quantity;
                return merged > MaxStackQuantity
                    ? new Result(Outcome.Rejected, null, 0)
                    : new Result(Outcome.Stacked, existingStack with { Quantity = merged }, 0);
            }

            return new Result(Outcome.Placed,
                BuildStack(groundItem, groundItem.Quantity, enchant, combine, refine, socket), 0);
        }

        return destinationSlot is not null
            ? new Result(Outcome.Rejected, null, 0)
            : new Result(Outcome.Placed, BuildStack(groundItem, 1, enchant, combine, refine, socket), 0);
    }

    private static ItemStack BuildStack(GroundItemEntity groundItem, int quantity, byte enchant, byte combine,
        byte refine, byte socket)
    {
        return new ItemStack(groundItem.ItemId, quantity, enchant, combine, refine, socket, groundItem.SocketGem1,
            groundItem.SocketGem2, groundItem.SocketGem3, 0, groundItem.SerialNumber);
    }

    public readonly record struct Result(Outcome Outcome, ItemStack? NewSlot, long MoneyAmount)
    {
        public bool Succeeded => Outcome is Outcome.Money or Outcome.Stacked or Outcome.Placed;
    }
}
