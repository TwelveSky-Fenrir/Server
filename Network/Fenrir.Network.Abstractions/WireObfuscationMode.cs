namespace Fenrir.Network.Abstractions;

public enum WireObfuscationMode : byte
{
    None,

    XorPacketGlobal,

    XorFieldAvatar
}
