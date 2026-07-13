using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Domain.Hotkeys;

public readonly record struct HotkeySlotWrite(byte Page, byte Index, HotkeySlot Slot);

public readonly record struct HotkeyMoveZoneCommand(
    int CharacterId,
    ImmutableArray<HotkeySlotWrite> HotkeyWrites,
    InventoryContainerSnapshot? InventoryContainer,
    TaskCompletionSource? Applied = null);
