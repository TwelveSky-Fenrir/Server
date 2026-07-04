namespace Fenrir.Generators.Analysis.Model;

/// <summary>Mirrors <c>Fenrir.Contracts.Wire.WireObfuscationMode</c>; the generator can't reference that assembly.</summary>
internal enum WireObfuscationMode : byte
{
    None,
    XorPacketGlobal,
    XorFieldAvatar
}
