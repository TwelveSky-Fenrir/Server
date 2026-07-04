using System.Collections.Immutable;

namespace Fenrir.Application.Game.Inventory;

/// <summary>
///     Pure, Zone-independent policy for CZ_PROCESS_DATA_SEND's "container move" tSort family: no I/O, no
///     Zone/PlayerRuntimeState dependency, independently unit-testable.
///     <para>
///         Container ids match game.CharacterItems.Container: 0/1 = the two Inventory pages (8x8, slots 0-63
///         each), 2 = Equipment (slots 0-12), 3/4 = the two Store pages (slots 0-27). Trade/Save are not
///         modeled here.
///     </para>
/// </summary>
public static class ContainerMatrix
{
    public enum MoveOutcome
    {
        Success,
        NoOp,
        SourceOutOfRange,
        DestinationOutOfRange,
        SourceEmpty,
        InsufficientQuantity
    }

    public const byte InventoryPage0 = 0;
    public const byte InventoryPage1 = 1;
    public const byte Equipment = 2;
    public const byte StorePage0 = 3;
    public const byte StorePage1 = 4;

    /// <summary>
    ///     Every tSort the legacy PROCESS_DATA switch recognizes. A value not in this set is fuzzing (legacy
    ///     disconnects); a value in this set Fenrir hasn't wired up yet must fail cleanly instead (see
    ///     <see cref="IsImplementedContainerMoveSort" />), never disconnect.
    /// </summary>
    private static readonly HashSet<int> KnownSorts =
    [
        // Progression / skills / stats.
        201, 202, 203, 204, 205, 206, 207, 233, 235, 236, 237, 239,

        // Container moves -- the family this type implements a subset of.
        208, 3000, 209, 210, 211, 253, 212, 252, 213, 214, 215, 216,
        218, 219, 220, 221, 222, 223, 250, 224, 248, 225, 226, 227,
        228, 251, 229, 249, 230, 231, 232,
        240, 241, 242, 243, 244, 245, 246, 247,
        254, 255, 256,

        // GM commands -- rank-gated, none implemented (no GM-rank concept yet).
        501, 502, 503, 504, 505, 523, 333, 506, 507, 508, 509, 510,
        511, 512, 513, 514, 515, 516, 517, 518, 519, 520, 521, 522,
        524, 525, 526, 527, 528,

        // Scripted duel, map 124 only.
        598, 599, 600, 601, 602, 603,

        // Pet XP / GM inventory maintenance.
        700, 701
    ];

    /// <summary>
    ///     The 3 container-move families this pass implements: inventory&lt;-&gt;inventory (208),
    ///     inventory-&gt;equipment (210), equipment-&gt;inventory (213). Every other recognized container-move
    ///     tSort (Store/Trade/Bank/Hotkey/1B-money/pet-bag/rune) is deliberately not implemented yet.
    /// </summary>
    private static readonly HashSet<int> ImplementedContainerMoveSorts = [208, 210, 213];

    public static bool IsKnownSort(int sort)
    {
        return KnownSorts.Contains(sort);
    }

    public static bool IsImplementedContainerMoveSort(int sort)
    {
        return ImplementedContainerMoveSorts.Contains(sort);
    }

    /// <summary>IsStackItemSafe: only these 2 item Sort values ever carry Quantity &gt; 1.</summary>
    public static bool IsStackableSort(byte itemSort)
    {
        return itemSort is 2 or 99;
    }

    public static bool TryGetMaxSlot(byte container, out int maxSlotInclusive)
    {
        switch (container)
        {
            case InventoryPage0:
            case InventoryPage1:
                maxSlotInclusive = 63;
                return true;
            case Equipment:
                maxSlotInclusive = 12;
                return true;
            case StorePage0:
            case StorePage1:
                maxSlotInclusive = 27;
                return true;
            default:
                maxSlotInclusive = 0;
                return false;
        }
    }

    public static bool IsValidSlot(byte container, int slot)
    {
        return slot >= 0 && TryGetMaxSlot(container, out var max) && slot <= max;
    }

    /// <summary>
    ///     Maps a container-move tSort + the wire's raw Page1/Page2 onto the actual (from,to) container pair.
    ///     "Page" only matters for the 2-page containers (Inventory/Store); whichever side of a 210/213 move
    ///     touches Equipment ignores its own Page field.
    /// </summary>
    public static bool TryResolveContainers(int sort, int page1, int page2, out byte fromContainer,
        out byte toContainer)
    {
        switch (sort)
        {
            case 208 when IsInventoryPage(page1) && IsInventoryPage(page2):
                fromContainer = (byte)page1;
                toContainer = (byte)page2;
                return true;

            case 210 when IsInventoryPage(page1):
                fromContainer = (byte)page1;
                toContainer = Equipment;
                return true;

            case 213 when IsInventoryPage(page2):
                fromContainer = Equipment;
                toContainer = (byte)page2;
                return true;

            default:
                fromContainer = 0;
                toContainer = 0;
                return false;
        }
    }

    private static bool IsInventoryPage(int page)
    {
        return page is InventoryPage0 or InventoryPage1;
    }

    /// <summary>
    ///     Move into an empty destination (splitting the source stack if requestedQuantity is less than the
    ///     full stack), merge into a destination holding the same stackable item, or swap the two whole stacks
    ///     otherwise (a documented, not independently source-verified, modeling choice).
    /// </summary>
    /// <param name="requestedQuantity">&lt;= 0 means "move the whole source stack".</param>
    public static MoveOutcomeResult ResolveMove(
        byte fromContainer, int fromSlot, int requestedQuantity,
        byte toContainer, int toSlot,
        ItemStack? source, ItemStack? destination,
        bool sourceIsStackable)
    {
        if (!IsValidSlot(fromContainer, fromSlot))
            return new MoveOutcomeResult(MoveOutcome.SourceOutOfRange, source, destination);

        if (!IsValidSlot(toContainer, toSlot))
            return new MoveOutcomeResult(MoveOutcome.DestinationOutOfRange, source, destination);

        if (source is not { } src)
            return new MoveOutcomeResult(MoveOutcome.SourceEmpty, source, destination);

        if (fromContainer == toContainer && fromSlot == toSlot)
            return new MoveOutcomeResult(MoveOutcome.NoOp, source, destination);

        var quantity = requestedQuantity <= 0 ? src.Quantity : requestedQuantity;
        if (quantity > src.Quantity)
            return new MoveOutcomeResult(MoveOutcome.InsufficientQuantity, source, destination);

        if (destination is not { } dst)
        {
            var moved = src with { Quantity = quantity };
            var remaining = src.Quantity - quantity;
            ItemStack? newSource = remaining > 0 ? src with { Quantity = remaining } : null;
            return new MoveOutcomeResult(MoveOutcome.Success, newSource, moved);
        }

        if (sourceIsStackable && dst.ItemId == src.ItemId)
        {
            var merged = dst with { Quantity = dst.Quantity + quantity };
            var remaining = src.Quantity - quantity;
            ItemStack? newSource = remaining > 0 ? src with { Quantity = remaining } : null;
            return new MoveOutcomeResult(MoveOutcome.Success, newSource, merged);
        }

        return new MoveOutcomeResult(MoveOutcome.Success, destination, source);
    }

    /// <summary>Projects a move onto the current container contents, producing the new full content of each side.</summary>
    public static ProjectedContainers ApplyMove(
        MoveOutcomeResult move,
        byte fromContainer, int fromSlot, ImmutableDictionary<byte, ItemStack> fromCurrent,
        byte toContainer, int toSlot, ImmutableDictionary<byte, ItemStack> toCurrent)
    {
        if (fromContainer == toContainer)
        {
            var updated = ApplySlotChange(fromCurrent, (byte)fromSlot, move.NewSource);
            updated = ApplySlotChange(updated, (byte)toSlot, move.NewDestination);
            return new ProjectedContainers(updated, updated);
        }

        var newFrom = ApplySlotChange(fromCurrent, (byte)fromSlot, move.NewSource);
        var newTo = ApplySlotChange(toCurrent, (byte)toSlot, move.NewDestination);
        return new ProjectedContainers(newFrom, newTo);
    }

    private static ImmutableDictionary<byte, ItemStack> ApplySlotChange(
        ImmutableDictionary<byte, ItemStack> current, byte slot, ItemStack? newValue)
    {
        return newValue is { } value ? current.SetItem(slot, value) : current.Remove(slot);
    }

    /// <summary>NewSource/NewDestination are the values to write back; null means "slot becomes empty".</summary>
    public readonly record struct MoveOutcomeResult(
        MoveOutcome Outcome,
        ItemStack? NewSource,
        ItemStack? NewDestination)
    {
        public bool Succeeded => Outcome is MoveOutcome.Success or MoveOutcome.NoOp;
    }

    /// <summary>One touched container's projected full new content -- see ApplyMove.</summary>
    public readonly record struct ProjectedContainers(
        ImmutableDictionary<byte, ItemStack> From,
        ImmutableDictionary<byte, ItemStack> To);
}
