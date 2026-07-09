using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TribeAnnouncementScroll,
    ExpectedSize = 79)]
public readonly record struct TribeAnnouncementScrollResponse : IOutgoingPacket
{
    /// <summary>TRAP: actually carries the sender's TRIBE NUMBER (1-4), not a role, despite the field name.</summary>
    public required int TribeRole { get; init; }

    [FixedString(13)] public required string AvatarName { get; init; }
    [FixedString(61)] public required string Content { get; init; }
}
