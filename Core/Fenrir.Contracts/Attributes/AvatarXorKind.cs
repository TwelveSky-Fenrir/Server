namespace Fenrir.Contracts.Attributes;

/// <summary><c>scopyAvtXor*</c> variant (§3.2) to apply to this field of <c>LcUserAvatarRecv2</c>.</summary>
public enum AvatarXorKind : byte
{
    None,
    Int,
    IntArray,
    Char,
    Char2
}
