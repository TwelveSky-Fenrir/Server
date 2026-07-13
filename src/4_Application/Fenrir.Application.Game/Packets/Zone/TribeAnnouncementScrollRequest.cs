using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;
using Fenrir.Application.Game.ZoneRuntime;

namespace Fenrir.Application.Game.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.TribeAnnouncementScroll,
    ExpectedSize = 70,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct TribeAnnouncementScrollRequest : IIncomingPacket<TribeAnnouncementScrollRequest>
{
    [FixedString(61)] public required string Content { get; init; }
}
