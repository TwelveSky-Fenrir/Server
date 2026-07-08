using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>Restricted to tribe master/vice-master; relayed through the center so every zone's tribe members get it.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.TribeAnnouncement, ExpectedSize = 70,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly record struct TribeAnnouncementRequest : IIncomingPacket<TribeAnnouncementRequest>
{
    [FixedString(61)] public required string Content { get; init; }
}
