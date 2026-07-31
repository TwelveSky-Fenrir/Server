using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GlobalAnnouncement, ExpectedSize = 62)]
public readonly partial record struct GlobalAnnouncementResponse : IOutgoingPacket
{
    [FixedString(61)] public required string Content { get; init; }
}
