using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TribeAnnouncementScroll,
    ExpectedSize = 79)]
public readonly partial record struct TribeAnnouncementScrollResponse : IOutgoingPacket
{
    /// <summary>TRAP: actually carries the sender's TRIBE NUMBER (1-4), not a role, despite the field name.</summary>
    public required int TribeRole { get; init; }

    [FixedString(13)] public required string AvatarName { get; init; }
    [FixedString(61)] public required string Content { get; init; }
}
