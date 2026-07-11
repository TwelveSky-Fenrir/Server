using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GlobalAnnouncement, ExpectedSize = 62)]
public readonly partial record struct GlobalAnnouncementResponse : IOutgoingPacket
{
    [FixedString(61)] public required string Content { get; init; }
}
