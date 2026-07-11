using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(4)]
public readonly partial record struct TeleportTollData : IFenrirWireType<TeleportTollData>
{
    public required int Money { get; init; }
}
