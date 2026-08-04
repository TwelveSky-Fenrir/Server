using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Simulation;

namespace Fenrir.Application.Game.Domain.Hotkeys;

public readonly record struct HotkeySlotWrite(byte Page, byte Index, HotkeySlot Slot);

public readonly record struct HotkeyMoveZoneCommand(
    int CharacterId,
    ImmutableArray<HotkeySlotWrite> HotkeyWrites,
    InventoryContainerSnapshot? InventoryContainer,
    TaskCompletionSource<ZoneCommandResult>? Applied = null);
