using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Domain.Hotkeys;

/// <summary>
///     One (page, index) hotkey slot's new content, unconditionally SET by <see cref="HotkeyMoveZoneCommand" />
///     -- <see cref="HotkeySlot.Empty" /> clears the slot, matching every other "row absence = unassigned key"
///     write in this family (<c>HotkeySlotMirrorZoneCommand</c>'s own precedent).
/// </summary>
public readonly record struct HotkeySlotWrite(byte Page, byte Index, HotkeySlot Slot);

/// <summary>
///     Posted by <c>Fenrir.Application.Game.Services.Hotkeys.HotkeyActionService</c> once a
///     CZ_PROCESS_DATA_SEND hotkey-bind-family request (tSort 204 skill/emoticon bind, 205 unbind, 211/253
///     inventory-to-hotkey item bind, 214 hotkey-to-inventory item withdraw, 216 hotkey-to-hotkey rearrange)
///     has already been resolved by <see cref="HotkeyActionResolver" /> and durably persisted --
///     <c>game.CharacterHotkeys</c> via <c>ICharacterRepository.UpsertHotkeySlotAsync</c> for every touched
///     slot in <see cref="HotkeyWrites" />, and <c>game.CharacterItems</c> via
///     <c>ICharacterRepository.ReplaceContainerAsync</c> for <see cref="InventoryContainer" /> when non-null.
///     Zone's tick just mirrors this already-decided result into the tick-owned
///     <see cref="Fenrir.Application.Game.Domain.World.PlayerRuntimeState" /> -- no validation, no I/O -- same
///     posture as <c>InventoryZoneCommand</c>/<c>HotkeySlotMirrorZoneCommand</c>
///     (<c>Zone.EconomyMirrors.cs</c>/<c>Zone.CosmeticMirrors.cs</c>). Kept as its own channel rather than
///     folded into either of those two unions, same "additive-only" rationale those two types' own remarks
///     already give for staying separate from <c>ZoneCommand</c>'s hand-discriminated core union.
/// </summary>
/// <param name="CharacterId">A no-op if the player already left the zone by the time the tick drains this.</param>
/// <param name="HotkeyWrites">
///     1-2 entries: cases 204/205/211/253/214 write exactly 1; case 216 writes 2 (source+destination) for a
///     genuine move, or is posted as a bare no-op success by the caller (never reaching this command at all)
///     for the source-equals-destination edge case -- see <c>HotkeyActionService.RearrangeAsync</c>'s own
///     remarks.
/// </param>
/// <param name="InventoryContainer">
///     Non-null only for tSort 211/253 (the source inventory slot debited/cleared) and 214 (the destination
///     inventory slot credited) -- the touched container's full new content (whole-container replace), same
///     shape as <see cref="InventoryContainerSnapshot" />.
/// </param>
/// <param name="Applied">
///     Completed the instant the tick actually mirrors this into <c>PlayerRuntimeState</c>, not merely when
///     posted -- callers must await this while already holding
///     <see cref="Fenrir.Application.Game.Domain.World.PlayerRuntimeState.EconomyActionLock" />, same contract
///     as <c>InventoryZoneCommand.Applied</c>.
/// </param>
public readonly record struct HotkeyMoveZoneCommand(
    int CharacterId,
    ImmutableArray<HotkeySlotWrite> HotkeyWrites,
    InventoryContainerSnapshot? InventoryContainer,
    TaskCompletionSource? Applied = null);
