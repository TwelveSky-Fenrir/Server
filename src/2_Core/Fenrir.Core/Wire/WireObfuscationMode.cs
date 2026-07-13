namespace Fenrir.Core.Wire;

/// <summary>
/// Mode d'obfuscation XOR d'un paquet (les 3 voies legacy). <see cref="XorPacketGlobal"/> = XOR du paquet
/// entier (générateur <c>GetMyXor</c>) ; <see cref="XorFieldAvatar"/> = XOR champ-par-champ de l'aperçu avatar.
/// </summary>
public enum WireObfuscationMode : byte
{
    None = 0,
    XorPacketGlobal = 1,
    XorFieldAvatar = 2
}
