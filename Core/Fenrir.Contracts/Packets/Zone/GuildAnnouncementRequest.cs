using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>Silently dropped unless the sender is the guild master (aGuildRole == 0).</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.GuildAnnouncement, ExpectedSize = 70,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct GuildAnnouncementRequest : IIncomingPacket<GuildAnnouncementRequest>
{
    [FixedString(61)] public required string Content { get; init; }
}
