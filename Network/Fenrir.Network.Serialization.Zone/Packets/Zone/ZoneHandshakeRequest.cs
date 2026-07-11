using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;
using Fenrir.Network.Serialization.Zone.Wire;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.ZoneHandshake, ExpectedSize = 272,
    AllowedStates = [(byte)ZoneSessionState.Connected])]
public readonly partial record struct ZoneHandshakeRequest : IIncomingPacket<ZoneHandshakeRequest>
{
    [FixedString(255)] public required string Id { get; init; }
    public required int Tribe { get; init; }
    public required int UserSort { get; init; }
}
