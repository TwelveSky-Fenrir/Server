using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>Local to the zone only, no inter-zone relay; recipients are same/allied tribe members.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TribeChat, ExpectedSize = 99)]
public readonly partial record struct TribeChatResponse : IOutgoingPacket
{
    [FixedString(13)] public required string AvatarName { get; init; }
    [FixedString(61)] public required string Content { get; init; }
    public required ItemLinkInfo Link { get; init; }
}
