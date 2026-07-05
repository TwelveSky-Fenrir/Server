using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.ZoneHandshake, ExpectedSize = 272,
    AllowedStates = [(byte)ZoneSessionState.Connected])]
public readonly partial record struct ZoneHandshakeRequest : IIncomingPacket<ZoneHandshakeRequest>
{
    // XOR USE_XOR_UID: de-obfuscated by the handler/Network layer, not by TryRead here.
    [FixedString(255)] public required string Id { get; init; }
    public required int Tribe { get; init; }
    public required int UserSort { get; init; }
}
