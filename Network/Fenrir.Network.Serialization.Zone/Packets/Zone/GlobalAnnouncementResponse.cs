using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

/// <summary>Legacy relay struct has a tTribe field that's never used; correctly absent from this wire type.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GlobalAnnouncement, ExpectedSize = 62)]
public readonly record struct GlobalAnnouncementResponse : IOutgoingPacket
{
    [FixedString(61)] public required string Content { get; init; }
}
