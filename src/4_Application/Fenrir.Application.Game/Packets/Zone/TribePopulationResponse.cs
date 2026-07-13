using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Game.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TribePopulation,
    ExpectedSize = 9)]
public readonly partial record struct TribePopulationResponse : IOutgoingPacket
{
    public required int ZoneNumber { get; init; }
    public required int ConnectedUser { get; init; }
}
