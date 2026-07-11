namespace Fenrir.Data.WriteBehind;

[Flags]
public enum DirtyFlags : byte
{
    None = 0,
    Position = 1 << 0,
    Vitals = 1 << 1,
    Progression = 1 << 2
}
