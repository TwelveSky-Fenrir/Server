using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TribePopulation,
    ExpectedSize = 9)]
public readonly partial record struct TribePopulationResponse : IOutgoingPacket
{
    public required int ZoneNumber { get; init; }
    public required int ConnectedUser { get; init; }
}
