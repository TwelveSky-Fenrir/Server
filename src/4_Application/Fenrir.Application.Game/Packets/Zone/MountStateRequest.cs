using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;
using Fenrir.Application.Game.ZoneRuntime;

namespace Fenrir.Application.Game.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.MountState, ExpectedSize = 17,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct MountStateRequest : IIncomingPacket<MountStateRequest>
{
    public required int Sort { get; init; }
    public required int Value { get; init; }
}
