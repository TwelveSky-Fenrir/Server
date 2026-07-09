using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;
using Fenrir.Network.Serialization.Zone.Wire;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

/// <summary>Consumes one scroll (aTribeNotifyNum); also pushes the updated count to the sender separately (ZC 22).</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.TribeAnnouncementScroll,
    ExpectedSize = 70,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly record struct TribeAnnouncementScrollRequest : IIncomingPacket<TribeAnnouncementScrollRequest>
{
    [FixedString(61)] public required string Content { get; init; }
}
