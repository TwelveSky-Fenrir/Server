using Fenrir.Core.Packets.Shared;
using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;
using Fenrir.Application.Game.ZoneRuntime;

namespace Fenrir.Application.Game.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.AutoHuntToggle, ExpectedSize = 125,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct AutoHuntToggleRequest : IIncomingPacket<AutoHuntToggleRequest>
{
    public required int Sort { get; init; }
    public required AutoHunt AutoHunt { get; init; }
}
