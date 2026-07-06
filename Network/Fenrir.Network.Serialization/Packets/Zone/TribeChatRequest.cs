using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>Local to the zone only (unlike the announcement opcode, no relay); reaches same/allied tribe members.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.TribeChat, ExpectedSize = 94,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct TribeChatRequest : IIncomingPacket<TribeChatRequest>
{
    [FixedString(61)] public required string Content { get; init; }
    public required ItemLinkInfo Link { get; init; }
}
