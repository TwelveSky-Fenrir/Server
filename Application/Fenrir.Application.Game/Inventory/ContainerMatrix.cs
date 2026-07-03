using System.Collections.Immutable;

namespace Fenrir.Application.Game.Inventory;

/// <summary>
///     Pure, Zone-independent policy for CZ_PROCESS_DATA_SEND's "container move" tSort family (report
///     04_mega_switches.md §1, ~50 uniform <c>MyWork::ProcessForXXX</c> methods sharing one contract:
///     <c>DEFAULT_PDATA_RECV{ tPage1,tIndex1,tQuantity1, tPage2,tIndex2,tXPost2,tYPost2 }</c> in, "FALSE +
///     Quit() on structural incoherence, else TRUE + *tResult" out). No I/O, no <c>Zone</c> dependency, no
///     <c>PlayerRuntimeState</c> dependency -- every input is a plain value the caller
///     (<c>GenericActionHandler</c>) already has in hand, which is what makes this independently
///     unit-testable (mission brief).
///     <para>
///     Container ids match <c>game.CharacterItems.Container</c>'s documented enum 1:1 (see that table's own
///     header comment): 0/1 = the two Inventory pages (8x8, slots 0-63 each), 2 = Equipment (slots 0-12,
///     MAX_EQUIP_SLOT_NUM=13), 3/4 = the two Store pages (slots 0-27, MAX_STORE_ITEM_SLOT_NUM=28). Trade/Save
///     are NOT modeled here -- see game.CharacterItems' own header comment for why.
///     </para>
/// </summary>
public static class ContainerMatrix
{
    public const byte InventoryPage0 = 0;
    public const byte InventoryPage1 = 1;
    public const byte Equipment = 2;
    public const byte StorePage0 = 3;
    public const byte StorePage1 = 4;

    /// <summary>
    ///     Every tSort the legacy PROCESS_DATA switch itself recognizes (report 04's 4 tables: progression/
    ///     skills, container moves, GM commands, scripted map-124 duel, pet/maintenance) -- a value NOT in this
    ///     set is what the legacy's own <c>default:</c> branch treats as fuzzing and disconnects for
    ///     (<c>Zone</c>-level <c>Abort</c>, reproduced by the handler). A value IN this set that
    ///     Fenrir has not wired up yet is a DIFFERENT case -- a legitimate, known feature simply out of this
    ///     pass's scope -- and must get a clean <c>tResult</c> failure instead (see
    ///     <see cref="IsImplementedContainerMoveSort" />), never a disconnect.
    /// </summary>
    private static readonly HashSet<int> KnownSorts =
    [
        // Progression / skills / stats (report 04 table 1).
        201, 202, 203, 204, 205, 206, 207, 233, 235, 236, 237, 239,

        // Container moves (report 04 table 2) -- the family this type actually implements a subset of.
        208, 3000, 209, 210, 211, 253, 212, 252, 213, 214, 215, 216,
        218, 219, 220, 221, 222, 223, 250, 224, 248, 225, 226, 227,
        228, 251, 229, 249, 230, 231, 232,
        240, 241, 242, 243, 244, 245, 246, 247,
        254, 255, 256,

        // GM commands (report 04 table 3) -- rank-gated, none implemented (no GM-rank concept yet).
        501, 502, 503, 504, 505, 523, 333, 506, 507, 508, 509, 510,
        511, 512, 513, 514, 515, 516, 517, 518, 519, 520, 521, 522,
        524, 525, 526, 527, 528,

        // Scripted duel, map 124 only (report 04 table 4).
        598, 599, 600, 601, 602, 603,

        // Pet XP / GM inventory maintenance.
        700, 701
    ];

    /// <summary>
    ///     The 3 "most fundamental" container-move families this pass implements (mission brief):
    ///     inventory&lt;-&gt;inventory (208), inventory-&gt;equipment (210), equipment-&gt;inventory (213).
    ///     Every other recognized container-move tSort (Store/Trade/Bank/Hotkey-assign/1B-money/pet-bag/rune)
    ///     is deliberately NOT implemented yet -- see <c>GenericActionHandler</c>'s own remarks and this
    ///     task's openIssues for the full list.
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

    /// <summary>IsStackItemSafe (report 04 §Empilage): only these 2 item Sort values ever carry Quantity &gt; 1.</summary>
    public static bool IsStackableSort(byte itemSort)
    {
        return itemSort is 2 or 99;
    }

    /// <summary>Highest legal slot index (inclusive) for <paramref name="container" />, or false if the container id itself is unknown.</summary>
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
    ///     Maps a container-move tSort + the wire's raw Page1/Page2 onto the actual (from,to) container pair
    ///     the generic <c>DEFAULT_PDATA_RECV</c> struct implies for THAT tSort. The wire struct has no explicit
    ///     "container kind" field of its own -- the kind is implied entirely by which tSort carried it, and
    ///     "Page" is only meaningful for the 2-page containers (Inventory/Store); Equipment has no pages, so
    ///     whichever side of a 210/213 move touches Equipment ignores its own Page field (report 04 §1 table 2:
    ///     "208 Inventaire→Inventaire", "210 Inventaire→Équipement", "213 Équipement→Inventaire").
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

    public enum MoveOutcome
    {
        Success,
        NoOp,
        SourceOutOfRange,
        DestinationOutOfRange,
        SourceEmpty,
        InsufficientQuantity
    }

    /// <summary><see cref="NewSource" />/<see cref="NewDestination" /> are the values to WRITE back; null means "slot becomes empty".</summary>
    public readonly record struct MoveOutcomeResult(MoveOutcome Outcome, ItemStack? NewSource, ItemStack? NewDestination)
    {
        public bool Succeeded => Outcome is MoveOutcome.Success or MoveOutcome.NoOp;
    }

    /// <summary>
    ///     The generic move/merge/swap mechanics shared by the ~50 legacy <c>ProcessForXXX</c> container-move
    ///     methods (report 04 §1): move into an empty destination (splitting the source stack when
    ///     <paramref name="requestedQuantity" /> is less than the full stack), merge into a destination holding
    ///     the SAME stackable item, or swap the two whole stacks when the destination holds a different item
    ///     (or the same ItemId but a non-stackable one, e.g. two distinct enchanted weapon instances that
    ///     happen to share an ItemId). The swap branch is a standard, defensible drag-and-drop rule -- report
    ///     04 does not fully transcribe <c>ProcessForInventoryToInventory</c>'s C++ body byte-for-bit, so this
    ///     is a DOCUMENTED, NOT independently verified, modeling choice (D8 divergence, flagged as an open
    ///     issue rather than silently assumed).
    /// </summary>
    /// <param name="requestedQuantity">&lt;= 0 means "move the whole source stack" (the common non-stackable case).</param>
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

        // Occupied by something that cannot merge with the source -- swap the two whole stacks (see remarks).
        return new MoveOutcomeResult(MoveOutcome.Success, destination, source);
    }

    /// <summary>One touched container's projected full new content -- see <see cref="ApplyMove" />.</summary>
    public readonly record struct ProjectedContainers(
        ImmutableDictionary<byte, ItemStack> From,
        ImmutableDictionary<byte, ItemStack> To);

    /// <summary>
    ///     Projects a <see cref="MoveOutcomeResult" /> onto the CURRENT full content of the touched
    ///     container(s), producing the NEW full content each side must be replaced with (whole-container
    ///     replace semantics, matching <c>usp_CharacterItems_ReplaceContainer</c>). Pure: does not touch
    ///     <see cref="InventoryState" /> itself -- the caller (handler, for SQL persistence; <c>Zone</c>, for
    ///     the in-memory mirror) decides when/whether to actually write the result back.
    /// </summary>
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
}
