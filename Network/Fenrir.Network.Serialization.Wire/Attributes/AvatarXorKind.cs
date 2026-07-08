namespace Fenrir.Network.Serialization.Wire.Attributes;

/// <summary><c>scopyAvtXor*</c> variant to apply to this field of <c>AvatarRosterResponse</c>.</summary>
public enum AvatarXorKind : byte
{
    None,
    Int,
    IntArray,
    Char,
    Char2
}
