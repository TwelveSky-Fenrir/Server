using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Inventory;

/// <summary>
///     Pure, Zone-independent policy for ground -> inventory pickup (ports MyWork::ProcessForGetItem,
///     Server/ts25zone/S04_MyWork05.cpp:250-373). Money is credited directly; a stackable item merges into a
///     same-id destination (capped at <see cref="MaxStackQuantity" />) or fills an empty slot; a non-stackable
///     item requires an empty destination -- there is no swap-with-occupant fallback (the legacy rejects
///     outright), the same reject-on-occupied-destination posture <see cref="ContainerMatrix.ResolveMove" />
///     itself now follows for its own family.
/// </summary>
/// <remarks>
///     Zone atomically claims the ground item before resolving this policy, so two players targeting the same
///     item can't duplicate it; a Rejected result therefore discards the claimed item instead of returning it
///     to the ground.
/// </remarks>
public static class GroundItemPickupPolicy
{
    public enum Outcome
    {
        /// <summary>iSort == 1 -- credit MoneyAmount to the killer's balance; no container slot touched.</summary>
        Money,

        /// <summary>Merged into an existing same-item stack.</summary>
        Stacked,

        /// <summary>Placed into a previously-empty destination slot.</summary>
        Placed,

        /// <summary>Destination occupied by an incompatible item, or a merge would exceed MaxStackQuantity.</summary>
        Rejected
    }

    /// <summary>CheckPossibleGetItem's pickup radius.</summary>
    public const float MaxPickupDistance = 100f;

    /// <summary>MAX_ITEM_DUPLICATION_NUM (DEFINE.h:611).</summary>
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
