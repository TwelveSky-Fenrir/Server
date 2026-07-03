using Fenrir.Data.Characters;

namespace Fenrir.Application.Game.Inventory;

/// <summary>
///     Builds <c>ObjectForAvatar.EquipForView</c> (report 30_Fenrir_ServerLogic/02_zc_inventory_0_98.md §8:
///     <c>int aEquipForView[13][2]</c>) from the Equipment container -- the reduced, OTHER-avatar-facing view
///     of what a character has equipped (as opposed to <c>AvatarInfo.Equip</c>'s full 4-int-per-slot state,
///     which only the OWNING client ever receives). Both <c>Zone.BuildAvatarActionRecv</c> (broadcast to
///     neighbors, reads live <see cref="ItemStack" />s) and <c>EnterWorldHandler</c>'s self-spawn
///     packet (reads freshly-loaded SQL rows, before the player is even tracked by a zone) need the exact same
///     {ItemId, Enchant} pairing, hence one shared codec.
/// </summary>
/// <remarks>
///     OPEN ISSUE (documented, not guessed): the exact per-slot sub-fields of <c>aEquipForView[13][2]</c> were
///     never isolated in the investigation reports (only the overall struct layout is confirmed). {ItemId,
///     Enchant} is the standard "what does this look like on another player" pairing for this class of legacy
///     protocol and is NOT independently verified byte-for-bit against the source -- flagged for a follow-up
///     verification pass rather than silently assumed to be exact.
/// </remarks>
public static class EquipmentViewCodec
{
    private const int EquipmentSlotCount = 13;

    /// <summary>From freshly-loaded world-entry SQL rows (RS1 of usp_Character_GetForWorldEntry, Container==2 only).</summary>
    public static int[] BuildEquipForView(IReadOnlyList<CharacterItemSlotDto> items)
    {
        var view = new int[EquipmentSlotCount * 2];

        foreach (var item in items)
        {
            if (item.Container != ContainerMatrix.Equipment || item.Slot >= EquipmentSlotCount)
                continue;

            view[item.Slot * 2] = item.ItemId;
            view[item.Slot * 2 + 1] = item.Enchant;
        }

        return view;
    }

    /// <summary>From the live in-memory Equipment container (a zone tick's own <see cref="InventoryState" />).</summary>
    public static int[] BuildEquipForView(IReadOnlyDictionary<byte, ItemStack> equipmentContainer)
    {
        var view = new int[EquipmentSlotCount * 2];

        foreach (var (slot, stack) in equipmentContainer)
        {
            if (slot >= EquipmentSlotCount)
                continue;

            view[slot * 2] = stack.ItemId;
            view[slot * 2 + 1] = stack.Enchant;
        }

        return view;
    }
}
