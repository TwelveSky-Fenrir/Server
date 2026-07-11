using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GuildAnnouncement, ExpectedSize = 75)]
public readonly partial record struct GuildAnnouncementResponse : IOutgoingPacket
{

        [FixedString(13)]
    public required string AvatarName { get; init; }

    [FixedString(61)] public required string Content { get; init; }
}
