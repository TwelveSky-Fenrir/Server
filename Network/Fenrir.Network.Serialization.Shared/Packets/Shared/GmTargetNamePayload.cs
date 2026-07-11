using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(13)]
public readonly partial record struct GmTargetNamePayload : IFenrirWireType<GmTargetNamePayload>
{
    [FixedString(13)] public required string TargetName { get; init; }
}
