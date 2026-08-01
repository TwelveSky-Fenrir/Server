using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.TribeAnnouncementScroll,
    ExpectedSize = 70,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct TribeAnnouncementScrollRequest : IIncomingPacket<TribeAnnouncementScrollRequest>
{
    [FixedString(61)] public required string Content { get; init; }
}
