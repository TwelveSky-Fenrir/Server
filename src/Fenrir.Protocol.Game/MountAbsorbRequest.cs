using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.MountAbsorb, ExpectedSize = 13,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct MountAbsorbRequest : IIncomingPacket<MountAbsorbRequest>
{
    public required int Sort { get; init; }
}
