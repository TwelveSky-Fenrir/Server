namespace Fenrir.Generators.Analysis.Support;

/// <summary>
///     Mirrors <c>Fenrir.Network.Serialization.Wire.WireHeaderSizes</c>; frame sizes hardcoded since codegen can't reference that
///     assembly.
/// </summary>
internal static class WireHeaderSizes
{
    public const int ClientPacketSize = 9;
    public const int DefaultPacketSize = 1;
}
