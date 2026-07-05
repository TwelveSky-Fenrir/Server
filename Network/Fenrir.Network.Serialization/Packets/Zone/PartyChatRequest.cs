using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.PartyChat, ExpectedSize = 94,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct PartyChatRequest : IIncomingPacket<PartyChatRequest>
{
    [FixedString(61)] public required string Content { get; init; }

    // Dead server-side: the relay never transports it, so decode but do not propagate downstream.
    public required ItemLinkInfo Link { get; init; }
}
