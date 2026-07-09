using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

/// <summary>
///     ZoneNumber is repurposed as a tribe id (348=Noble Dragon, 349=Royal Serpent, 350=Grand Tiger, 351=Nangin),
///     never a real zone number.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TribePopulation,
    ExpectedSize = 9)]
public readonly record struct TribePopulationResponse : IOutgoingPacket
{
    public required int ZoneNumber { get; init; }
    public required int ConnectedUser { get; init; }
}
