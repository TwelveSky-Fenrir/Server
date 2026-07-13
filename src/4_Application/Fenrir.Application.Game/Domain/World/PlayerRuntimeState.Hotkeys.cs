using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Hotkeys;

namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    public ImmutableDictionary<(byte Page, byte Index), HotkeySlot> Hotkeys { get; set; } =
        ImmutableDictionary<(byte, byte), HotkeySlot>.Empty;

    public HotkeySlot GetHotkeySlot(byte page, byte index)
    {
        return Hotkeys.TryGetValue((page, index), out var slot) ? slot : HotkeySlot.Empty;
    }

    public void SetHotkeySlot(byte page, byte index, HotkeySlot slot)
    {
        Hotkeys = slot.IsEmpty ? Hotkeys.Remove((page, index)) : Hotkeys.SetItem((page, index), slot);
    }
}
