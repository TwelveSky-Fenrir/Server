using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.Inventory;

/// <summary>
///     One pet-bag slot's new content, unconditionally SET by <see cref="PetBagZoneCommand" /> -- a null
///     <see cref="ItemId" /> clears the slot, matching the same "row absence = unassigned slot" convention
///     <c>HotkeySlotWrite</c> uses for the sibling hotkey-bind family.
/// </summary>
public readonly record struct PetBagSlotWrite(byte Slot, int? ItemId);

/// <summary>
///     Posted by <c>Fenrir.Application.Game.Services.Inventory.PetBagActionService</c> once a
///     CZ_PROCESS_DATA_SEND pet-bag-family request (tSort 254 deposit, 255 withdraw, 256 rearrange) has
///     already been resolved by <see cref="PetBagItemTransferPolicy" /> and durably persisted (a NEW
///     <c>IPetBagRepository</c> -- see that service's own remarks). Zone's tick just mirrors this
///     already-decided result into the tick-owned
///     <see cref="Fenrir.Application.Game.Domain.World.PlayerRuntimeState" /> -- no validation, no I/O -- same
///     posture as <c>InventoryZoneCommand</c>/<c>HotkeyMoveZoneCommand</c>.
/// </summary>
/// <param name="CharacterId">A no-op if the player already left the zone by the time the tick drains this.</param>
/// <param name="PetBagWrites">
///     1 entry for tSort 254/255 (the single touched bag slot); 2 entries for tSort 256 (source cleared,
///     destination set) -- a genuine source-equals-destination rearrange no-op is never posted at all, see
///     <c>PetBagActionService.RearrangeAsync</c>'s own remarks.
/// </param>
/// <param name="InventoryContainer">
///     Non-null only for tSort 254 (source inventory slot cleared) and 255 (destination inventory slot
///     credited) -- the touched container's full new content (whole-container replace), same shape as
///     <see cref="InventoryContainerSnapshot" />.
/// </param>
/// <param name="Applied">
///     Completed the instant the tick actually mirrors this, not merely when posted -- callers must await this
///     while already holding
///     <see cref="Fenrir.Application.Game.Domain.World.PlayerRuntimeState.EconomyActionLock" />, same contract
///     as <c>InventoryZoneCommand.Applied</c>.
/// </param>
public readonly record struct PetBagZoneCommand(
    int CharacterId,
    ImmutableArray<PetBagSlotWrite> PetBagWrites,
    InventoryContainerSnapshot? InventoryContainer,
    TaskCompletionSource? Applied = null);
