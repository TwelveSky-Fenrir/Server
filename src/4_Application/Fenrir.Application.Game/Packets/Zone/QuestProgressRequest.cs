using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;
using Fenrir.Application.Game.ZoneRuntime;

namespace Fenrir.Application.Game.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.QuestProgress, ExpectedSize = 29,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct QuestProgressRequest : IIncomingPacket<QuestProgressRequest>
{
    public required int Sort { get; init; }
    public required int Page1 { get; init; }
    public required int Index1 { get; init; }
    public required int XPost { get; init; }
    public required int YPost { get; init; }
}
