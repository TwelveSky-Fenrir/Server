namespace Fenrir.Generators.Analysis.Model;

/// <summary>
///     Local mirror of <c>Fenrir.Contracts.Wire.WireObfuscationMode</c> (the generator doesn't reference the assembly
///     it analyzes).
/// </summary>
internal enum WireObfuscationMode : byte
{
    None,
    XorPacketGlobal,
    XorFieldAvatar
}
