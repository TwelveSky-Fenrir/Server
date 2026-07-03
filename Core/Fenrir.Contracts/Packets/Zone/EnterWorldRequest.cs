using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.EnterWorld,
    ExpectedSize = 381, AllowedStates = [(byte)ZoneSessionState.TicketConsumed])]
public readonly partial record struct EnterWorldRequest : IIncomingPacket<EnterWorldRequest>
{
    [FixedString(255)] public required string Id { get; init; }
    [FixedString(13)] public required string AvatarName { get; init; }
    public required ActionInfo Action { get; init; }
}
