namespace Fenrir.Generators.Analysis.Model;

/// <summary>
///     Local mirror of <c>Fenrir.Contracts.Wire.LegacyObfuscation</c> (the generator doesn't reference the assembly
///     it analyzes).
/// </summary>
internal enum LegacyObfuscation : byte
{
    None,
    XorPacketGlobal,
    XorFieldAvatar
}
