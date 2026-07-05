using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>ZoneNumber is ignored by the handler; it always replies with counts for tribes 0-3 of the current process.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.TribePopulation,
    ExpectedSize = 13, AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct TribePopulationRequest : IIncomingPacket<TribePopulationRequest>
{
    public required int ZoneNumber { get; init; }
}
