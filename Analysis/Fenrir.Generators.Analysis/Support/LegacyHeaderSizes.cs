namespace Fenrir.Generators.Analysis.Support;

/// <summary>
///     Local mirror of <c>Fenrir.Contracts.Wire.LegacyHeaders</c> — the generator can't reference the assembly it
///     analyzes, so frame sizes are hardcoded here for codegen.
/// </summary>
internal static class LegacyHeaderSizes
{
    public const int ClientPacketSize = 9;
    public const int DefaultPacketSize = 1;
}
