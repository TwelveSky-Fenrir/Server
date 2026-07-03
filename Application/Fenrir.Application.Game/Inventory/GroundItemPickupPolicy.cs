using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.World.Loot;

namespace Fenrir.Application.Game.Inventory;

/// <summary>
///     Pure, Zone-independent policy for CZ_PROCESS_DATA_SEND tSort 201 (ground -&gt; inventory pickup, report
///     ServerDocs/30_Fenrir_ServerLogic/04_mega_switches.md line 40 / 05_game_mechanics.md §5) -- the
///     ground-item twin of <see cref="ContainerMatrix" />'s own container-move policy: no I/O, no
///     <see cref="Fenrir.Application.Game.World.Zone" /> dependency, independently unit-testable. Ports
///     <c>MyWork::ProcessForGetItem</c>'s destination-resolution switch (verified against source,
///     <c>Server/ts25zone/S04_MyWork05.cpp:250-373</c>): money is credited directly (never touches a
///     container), a stackable item merges into a same-id destination (bounded
///     <c>MAX_ITEM_DUPLICATION_NUM = 999</c>) or fills an empty slot, and a non-stackable (equipment/unique)
///     item requires an EMPTY destination slot -- unlike <see cref="ContainerMatrix.ResolveMove" />, there is
///     no swap-with-occupant fallback here (the legacy rejects outright: <c>if (to-&gt;iIndex &gt; 0) return FALSE;</c>).
/// </summary>
/// <remarks>
///     OPEN ISSUE (documented, not guessed): <c>ProcessForGetItem</c>'s body (as read during this
///     investigation) never calls a <c>Free()</c>/invalidate on the ground item object itself -- its exact
///     removal-from-the-world mechanism was not conclusively located. This pass's <see cref="World.Zone" />
///     therefore claims (atomically removes) the ground item BEFORE resolving this policy, for race-safety
///     against two players targeting the same item concurrently (see <see cref="World.Zone.TryClaimGroundItem" />'s
///     own remarks); a resulting <see cref="Outcome.Rejected" /> (destination occupied by an incompatible
///     item) therefore discards the claimed item rather than returning it to the ground -- a deliberate, safe
///     (never duplicates, never crashes) simplification for a case a well-behaved client should not trigger
///     in practice (it always targets a slot it believes compatible/empty).
/// </remarks>
public static class GroundItemPickupPolicy
{
    /// <summary><c>CheckPossibleGetItem</c>'s pickup radius (report 05 §5: "distance max 100.0f").</summary>
    public const float MaxPickupDistance = 100f;

    /// <summary><c>MAX_ITEM_DUPLICATION_NUM</c> (DEFINE.h:611) -- the stack cap a merge must not exceed.</summary>
    public const int MaxStackQuantity = 999;

    public enum Outcome
    {
        /// <summary>iSort == 1 -- credit <see cref="Result.MoneyAmount" /> to the killer's balance; no container slot touched.</summary>
        Money,

        /// <summary>Merged into an existing same-item stack.</summary>
        Stacked,

        /// <summary>Placed into a previously-empty destination slot.</summary>
        Placed,

        /// <summary>Destination occupied by an incompatible item, or a stack merge would exceed <see cref="MaxStackQuantity" /> -- see class remarks.</summary>
        Rejected
    }

    public readonly record struct Result(Outcome Outcome, ItemStack? NewSlot, long MoneyAmount)
    {
        public bool Succeeded => Outcome is Outcome.Money or Outcome.Stacked or Outcome.Placed;
    }

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

            return new Result(Outcome.Placed, BuildStack(groundItem, groundItem.Quantity, enchant, combine, refine, socket), 0);
        }

        // Non-stackable (equipment/unique): destination MUST already be empty -- no swap fallback (see class remarks).
        return destinationSlot is not null
            ? new Result(Outcome.Rejected, null, 0)
            : new Result(Outcome.Placed, BuildStack(groundItem, 1, enchant, combine, refine, socket), 0);
    }

    private static ItemStack BuildStack(GroundItemEntity groundItem, int quantity, byte enchant, byte combine,
        byte refine, byte socket)
    {
        return new ItemStack(groundItem.ItemId, quantity, enchant, combine, refine, socket, groundItem.SocketGem1,
            groundItem.SocketGem2, groundItem.SocketGem3, ExpireDate: 0, groundItem.SerialNumber);
    }
}
