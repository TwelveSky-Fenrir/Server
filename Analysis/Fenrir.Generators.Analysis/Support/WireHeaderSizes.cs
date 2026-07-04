namespace Fenrir.Generators.Analysis.Support;

/// <summary>
///     Local mirror of <c>Fenrir.Contracts.Wire.WireHeaderSizes</c> — the generator can't reference the assembly it
///     analyzes, so frame sizes are hardcoded here for codegen.
/// </summary>
internal static class WireHeaderSizes
{
    public const int ClientPacketSize = 9;
    public const int DefaultPacketSize = 1;
}
