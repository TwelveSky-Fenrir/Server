namespace Fenrir.Generators.Analysis.Model;

/// <summary>Mirrors <c>Fenrir.Network.Serialization.Wire.WireObfuscationMode</c>; the generator can't reference that assembly.</summary>
internal enum WireObfuscationMode : byte
{
    None,
    XorPacketGlobal,
    XorFieldAvatar
}
