using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.DuelStart, ExpectedSize = 9,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct DuelStartRequest : IIncomingPacket<DuelStartRequest>
{
}
