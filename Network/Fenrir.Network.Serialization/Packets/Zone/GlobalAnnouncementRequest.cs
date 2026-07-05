using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>Silently dropped by the handler unless the sender is GM (uUserSort &lt; 1).</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.GlobalAnnouncement, ExpectedSize = 70,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct GlobalAnnouncementRequest : IIncomingPacket<GlobalAnnouncementRequest>
{
    [FixedString(61)] public required string Content { get; init; }
}
