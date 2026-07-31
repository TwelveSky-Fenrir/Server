using Fenrir.Core.Attributes;
using Fenrir.Core.Packets.Shared;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.AutoHuntToggle, ExpectedSize = 125,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct AutoHuntToggleRequest : IIncomingPacket<AutoHuntToggleRequest>
{
    public required int Sort { get; init; }
    public required AutoHunt AutoHunt { get; init; }
}
