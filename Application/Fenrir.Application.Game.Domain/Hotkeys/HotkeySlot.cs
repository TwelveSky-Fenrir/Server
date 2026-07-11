namespace Fenrir.Application.Game.Domain.Hotkeys;

public enum HotkeyBindingKind : byte
{
    None = 0,
    Skill = 1,
    Emoticon = 2,
    Item = 3
}

public readonly record struct HotkeySlot(HotkeyBindingKind Kind, int Value1, int Value2)
{
    public static readonly HotkeySlot Empty = new(HotkeyBindingKind.None, 0, 0);

    public bool IsEmpty => Kind == HotkeyBindingKind.None;
}
