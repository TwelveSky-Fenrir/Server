namespace Fenrir.Core.Wire;

public enum WireObfuscationMode : byte
{
    None = 0,
    XorPacketGlobal = 1,
    XorFieldAvatar = 2
}
