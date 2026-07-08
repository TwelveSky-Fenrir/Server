namespace Fenrir.Generators.Analysis.Support;

/// <summary>Fully-qualified names on the never-referenced <c>Fenrir.Contracts</c> side, for Roslyn symbol resolution.</summary>
internal static class WellKnownNames
{
    public const string FenrirPacketAttribute = "Fenrir.Network.Serialization.Wire.Attributes.FenrirPacketAttribute";

    public const string FenrirWireTypeAttribute =
        "Fenrir.Network.Serialization.Wire.Attributes.FenrirWireTypeAttribute";

    public const string FixedStringAttribute = "Fenrir.Network.Serialization.Wire.Attributes.FixedStringAttribute";
    public const string FixedArrayAttribute = "Fenrir.Network.Serialization.Wire.Attributes.FixedArrayAttribute";
    public const string ReservedAttribute = "Fenrir.Network.Serialization.Wire.Attributes.ReservedAttribute";

    public const string ObfuscatedUidFieldAttribute =
        "Fenrir.Network.Serialization.Wire.Attributes.ObfuscatedUidFieldAttribute";

    public const string AvatarXorKindAttribute =
        "Fenrir.Network.Serialization.Wire.Attributes.AvatarXorKindAttribute";

    public const string IFenrirWireType = "Fenrir.Network.Abstractions.IFenrirWireType`1";
    public const string IIncomingPacket = "Fenrir.Network.Abstractions.IIncomingPacket`1";
    public const string IOutgoingPacket = "Fenrir.Network.Abstractions.IOutgoingPacket";
    public const string IInlinePacketHandler = "Fenrir.Network.Abstractions.IInlinePacketHandler`1";
    public const string IAsyncPacketHandler = "Fenrir.Network.Abstractions.IAsyncPacketHandler`1";
    public const string IPacketSession = "Fenrir.Network.Abstractions.IPacketSession";

    public const string WireXor = "global::Fenrir.Network.Compression.WireXor";
    public const string WireHeaderSizes = "global::Fenrir.Network.Serialization.Wire.WireHeaderSizes";
    public const string FenrirServerEnum = "global::Fenrir.Network.Abstractions.FenrirServer";
    public const string FenrirDirectionEnum = "global::Fenrir.Network.Serialization.Wire.FenrirDirection";
    public const string LoginSessionStateEnum = "global::Fenrir.Network.Serialization.Login.Wire.LoginSessionState";
    public const string ZoneSessionStateEnum = "global::Fenrir.Network.Serialization.Zone.Wire.ZoneSessionState";
}
