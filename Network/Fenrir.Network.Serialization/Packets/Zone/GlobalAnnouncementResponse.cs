using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>Legacy relay struct has a tTribe field that's never used; correctly absent from this wire type.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GlobalAnnouncement, ExpectedSize = 62)]
public readonly partial record struct GlobalAnnouncementResponse : IOutgoingPacket
{
    [FixedString(61)] public required string Content { get; init; }
}
