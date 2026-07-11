using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(4)]
public readonly partial record struct GmTribeChangePayload : IFenrirWireType<GmTribeChangePayload>
{
    public required int Tribe { get; init; }
}
