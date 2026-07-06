namespace Fenrir.Application.Game.Domain.Hotkeys;

/// <summary>
///     Which of a hotkey slot's two tagged-union meanings is currently populated. Mirrors the legacy
///     <c>HOTKEY_SORT</c> enum's three accessor views over the same 3-int slot memory (skill/emoticon/item),
///     collapsed here to the discriminator only: <see cref="HotkeySlot.Value1" />/<see cref="HotkeySlot.Value2" />
///     carry a different meaning per <see cref="HotkeySlot.Kind" /> (SkillId+Grade, EmoticonCode+0, or
///     ItemId+Quantity). The numeric values assigned to this enum are a Fenrir-internal convention, not a
///     transcription of the legacy enum's own numbering -- the behavior contract this type implements
///     deliberately does not state the legacy wire-level binding-kind codes, so none are guessed here. Whoever
///     wires the actual wire packet maps its raw kind code onto this enum (rejecting anything that isn't
///     <see cref="Skill" /> or <see cref="Emoticon" /> for a bind request, per the contract).
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:122-159 (HOTKEY_SORT enum and the three tagged-union
///     accessors) ; Server/Header/Protocol/DEFINE.h:287-303 (MAX_HOT_KEY_PAGE=3, MAX_HOT_KEY_NUM=14,
///     MAX_HOT_KEY_VALUE=3).
/// </remarks>
public enum HotkeyBindingKind : byte
{
    None = 0,
    Skill = 1,
    Emoticon = 2,
    Item = 3
}

/// <summary>
///     One hotkey slot's raw stored triple. The (page, index) address is the caller's own dictionary key, not
///     repeated here -- same convention as <see cref="Inventory.ItemStack" /> for its container/slot.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:65-70 (<c>ClearHotkey</c> -- the shared "zero a slot"
///     helper <see cref="Empty" /> mirrors).
/// </remarks>
public readonly record struct HotkeySlot(HotkeyBindingKind Kind, int Value1, int Value2)
{
    public static readonly HotkeySlot Empty = new(HotkeyBindingKind.None, 0, 0);

    public bool IsEmpty => Kind == HotkeyBindingKind.None;
}
