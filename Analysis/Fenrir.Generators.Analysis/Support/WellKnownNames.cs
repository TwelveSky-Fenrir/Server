namespace Fenrir.Generators.Analysis.Support;

/// <summary>
///     Fully-qualified names on the <c>Fenrir.Contracts</c> side (the analyzed assembly, never referenced by this
///     netstandard2.0 generator) used for Roslyn attribute/symbol resolution.
/// </summary>
internal static class WellKnownNames
{
    public const string FenrirPacketAttribute = "Fenrir.Contracts.Attributes.FenrirPacketAttribute";
    public const string FenrirWireTypeAttribute = "Fenrir.Contracts.Attributes.FenrirWireTypeAttribute";
    public const string FixedStringAttribute = "Fenrir.Contracts.Attributes.FixedStringAttribute";
    public const string FixedArrayAttribute = "Fenrir.Contracts.Attributes.FixedArrayAttribute";
    public const string ReservedAttribute = "Fenrir.Contracts.Attributes.ReservedAttribute";
    public const string ObfuscatedUidFieldAttribute = "Fenrir.Contracts.Attributes.ObfuscatedUidFieldAttribute";
    public const string AvatarXorKindAttribute = "Fenrir.Contracts.Attributes.AvatarXorKindAttribute";

    public const string IFenrirWireType = "Fenrir.Contracts.Abstractions.IFenrirWireType`1";
    public const string IIncomingPacket = "Fenrir.Contracts.Abstractions.IIncomingPacket`1";
    public const string IOutgoingPacket = "Fenrir.Contracts.Abstractions.IOutgoingPacket";
    public const string IInlinePacketHandler = "Fenrir.Contracts.Abstractions.IInlinePacketHandler`1";
    public const string IAsyncPacketHandler = "Fenrir.Contracts.Abstractions.IAsyncPacketHandler`1";
    public const string IPacketSession = "Fenrir.Contracts.Abstractions.IPacketSession";

    public const string WireXor = "global::Fenrir.Contracts.Wire.WireXor";
    public const string WireHeaderSizes = "global::Fenrir.Contracts.Wire.WireHeaderSizes";
    public const string FenrirServerEnum = "global::Fenrir.Contracts.Wire.FenrirServer";
    public const string FenrirDirectionEnum = "global::Fenrir.Contracts.Wire.FenrirDirection";
    public const string LoginSessionStateEnum = "global::Fenrir.Contracts.Wire.LoginSessionState";
    public const string ZoneSessionStateEnum = "global::Fenrir.Contracts.Wire.ZoneSessionState";
}
