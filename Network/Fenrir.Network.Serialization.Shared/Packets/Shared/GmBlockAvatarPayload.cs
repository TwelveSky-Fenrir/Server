using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(13)]
public readonly partial record struct GmBlockAvatarPayload : IFenrirWireType<GmBlockAvatarPayload>
{
    [FixedString(13)] public required string AvatarName { get; init; }
}
