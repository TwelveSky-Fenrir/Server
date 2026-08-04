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

    public const int MaxStackQuantity = ItemQuantityPolicy.MaxStackQuantity;

    public static Result Resolve(ItemDefinition itemDefinition, GroundItemEntity groundItem,
        ItemStack? destinationSlot, byte requestedX, byte requestedY)
    {
        var sort = itemDefinition.Item.Sort;

        if (sort == 1)
            return groundItem.Quantity is >= ItemQuantityPolicy.MinStackQuantity and <=
                (int)InventoryToWorldDropPolicy.MaxNumberSentinel
                ? new Result(Outcome.Money, null, groundItem.Quantity)
                : new Result(Outcome.Rejected, null, 0);

        var (enchant, combine, refine, socket) = ItemValueCodec.Decode(groundItem.Value);

        if (ContainerMatrix.IsStackableSort(sort))
        {
            if (groundItem.Quantity < ItemQuantityPolicy.MinStackQuantity ||
                groundItem.Quantity > MaxStackQuantity)
                return new Result(Outcome.Rejected, null, 0);

            if (destinationSlot is { } existingStack)
            {
                if (existingStack.ItemId != groundItem.ItemId ||
                    existingStack.Quantity is < ItemQuantityPolicy.MinStackQuantity or > MaxStackQuantity)
                    return new Result(Outcome.Rejected, null, 0);

                var merged = existingStack.Quantity + groundItem.Quantity;
                return merged > MaxStackQuantity
                    ? new Result(Outcome.Rejected, null, 0)
                    : new Result(Outcome.Stacked, existingStack with { Quantity = merged }, 0);
            }

            return new Result(Outcome.Placed,
                BuildStack(groundItem, groundItem.Quantity, enchant, combine, refine, socket, requestedX,
                    requestedY), 0);
        }

        var placedQuantity = ResolveNonStackableQuantity(sort, groundItem.Quantity);
        if (placedQuantity is null)
            return new Result(Outcome.Rejected, null, 0);

        return destinationSlot is not null
            ? new Result(Outcome.Rejected, null, 0)
            : new Result(Outcome.Placed,
                BuildStack(groundItem, placedQuantity.Value, enchant, combine, refine, socket, requestedX,
                    requestedY), 0);
    }

    private static int? ResolveNonStackableQuantity(byte itemSort, int quantity)
    {
        return itemSort switch
        {
            ItemQuantityPolicy.PetSort when quantity is >= ItemQuantityPolicy.MinStackQuantity and <=
                ItemQuantityPolicy.MaxPetActivity => quantity,
            _ when quantity == 1 => 1,
            _ => null
        };
    }

    private static ItemStack BuildStack(GroundItemEntity groundItem, int quantity, byte enchant, byte combine,
        byte refine, byte socket, byte xPos, byte yPos)
    {
        return new ItemStack(groundItem.ItemId, quantity, enchant, combine, refine, socket, groundItem.SocketGem1,
            groundItem.SocketGem2, groundItem.SocketGem3, 0, groundItem.SerialNumber, xPos, yPos);
    }

    public readonly record struct Result(Outcome Outcome, ItemStack? NewSlot, long MoneyAmount)
    {
        public bool Succeeded => Outcome is Outcome.Money or Outcome.Stacked or Outcome.Placed;
    }
}
