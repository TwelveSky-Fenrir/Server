using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>Local to the zone only (unlike the announcement opcode, no relay); reaches same/allied tribe members.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.TribeChat, ExpectedSize = 94,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct TribeChatRequest : IIncomingPacket<TribeChatRequest>
{
    [FixedString(61)] public required string Content { get; init; }
    public required ItemLinkInfo Link { get; init; }
}
