namespace Fenrir.Generators.Analysis.Support;

internal static class WellKnownNames
{
    public const string FenrirPacketAttribute = "Fenrir.Core.Attributes.FenrirPacketAttribute";

    public const string FenrirWireTypeAttribute = "Fenrir.Core.Attributes.FenrirWireTypeAttribute";

    public const string FixedStringAttribute = "Fenrir.Core.Attributes.FixedStringAttribute";
    public const string FixedArrayAttribute = "Fenrir.Core.Attributes.FixedArrayAttribute";
    public const string ReservedAttribute = "Fenrir.Core.Attributes.ReservedAttribute";

    public const string ObfuscatedUidFieldAttribute = "Fenrir.Core.Attributes.ObfuscatedUidFieldAttribute";

    public const string AvatarXorKindAttribute = "Fenrir.Core.Attributes.AvatarXorKindAttribute";

    public const string IFenrirWireType = "Fenrir.Core.Abstractions.IFenrirWireType`1";
    public const string IIncomingPacket = "Fenrir.Core.Abstractions.IIncomingPacket`1";
    public const string IOutgoingPacket = "Fenrir.Core.Abstractions.IOutgoingPacket";
    public const string IInlinePacketHandler = "Fenrir.Core.Abstractions.IInlinePacketHandler`1";
    public const string IAsyncPacketHandler = "Fenrir.Core.Abstractions.IAsyncPacketHandler`1";


    public const string IPacketSession = "global::Fenrir.Core.Abstractions.IPacketSession";

    public const string WireXor = "global::Fenrir.Core.Wire.WireXor";
    public const string WireHeaderSizes = "global::Fenrir.Core.Wire.WireHeaderSizes";
    public const string MessageReader = "global::Fenrir.Core.Wire.MessageReader";
    public const string MessageWriter = "global::Fenrir.Core.Wire.MessageWriter";
    public const string WireObfuscationModeEnum = "global::Fenrir.Core.Wire.WireObfuscationMode";
    public const string FenrirServerEnum = "global::Fenrir.Core.Wire.FenrirServer";
    public const string FenrirDirectionEnum = "global::Fenrir.Core.Wire.FenrirDirection";

    public const string IOpcodeFrameSizeProvider = "global::Fenrir.Core.Abstractions.IOpcodeFrameSizeProvider";


    public const string LoginSessionStateEnum = "global::Fenrir.Protocol.Login.LoginSessionState";

    public const string ZoneSessionStateEnum = "global::Fenrir.Protocol.Game.ZoneSessionState";
}
