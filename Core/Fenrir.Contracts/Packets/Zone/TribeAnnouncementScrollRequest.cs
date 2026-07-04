using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>Consumes one scroll (aTribeNotifyNum); also pushes the updated count to the sender separately (ZC 22).</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.TribeAnnouncementScroll,
    ExpectedSize = 70,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct TribeAnnouncementScrollRequest : IIncomingPacket<TribeAnnouncementScrollRequest>
{
    [FixedString(61)] public required string Content { get; init; }
}
