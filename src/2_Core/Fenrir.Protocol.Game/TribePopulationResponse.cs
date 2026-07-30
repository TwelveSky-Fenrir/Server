using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TribePopulation,
    ExpectedSize = 9)]
public readonly partial record struct TribePopulationResponse : IOutgoingPacket
{
    public required int ZoneNumber { get; init; }
    public required int ConnectedUser { get; init; }
}
