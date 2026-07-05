using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.EnterWorld,
    ExpectedSize = 381, AllowedStates = [(byte)ZoneSessionState.TicketConsumed])]
public readonly partial record struct EnterWorldRequest : IIncomingPacket<EnterWorldRequest>
{
    [FixedString(255)] public required string Id { get; init; }
    [FixedString(13)] public required string AvatarName { get; init; }
    public required ActionInfo Action { get; init; }
}
