using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Hotkeys;

namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    /// <summary>
    ///     wAvatar.aHotKey[3][14][3] mirror (<see cref="Hotkeys.HotkeyActionResolver.PageCount" /> x
    ///     <see cref="Hotkeys.HotkeyActionResolver.SlotsPerPage" />) -- an absent key is an unassigned slot
    ///     (<see cref="HotkeySlot.Empty" />), same "absence = empty" convention as <see cref="Inventory" />'s
    ///     own per-container dictionaries. Populated at world entry/zone transfer from the character's
    ///     persisted <c>game.CharacterHotkeys</c> rows (see <c>EnterWorldService</c>/<c>ZoneTransfer.CreateEnterData</c>)
    ///     and mutated only by <see cref="Zone" />'s own tick, same single-writer contract as every other field
    ///     on this type.
    /// </summary>
    public ImmutableDictionary<(byte Page, byte Index), HotkeySlot> Hotkeys { get; set; } =
        ImmutableDictionary<(byte, byte), HotkeySlot>.Empty;

    public HotkeySlot GetHotkeySlot(byte page, byte index)
    {
        return Hotkeys.TryGetValue((page, index), out var slot) ? slot : HotkeySlot.Empty;
    }

    /// <summary>Removes the key entirely for an empty slot rather than storing <see cref="HotkeySlot.Empty" /> in place.</summary>
    public void SetHotkeySlot(byte page, byte index, HotkeySlot slot)
    {
        Hotkeys = slot.IsEmpty ? Hotkeys.Remove((page, index)) : Hotkeys.SetItem((page, index), slot);
    }
}
