using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.QuestProgress, ExpectedSize = 29,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct QuestProgressRequest : IIncomingPacket<QuestProgressRequest>
{
    public required int Sort { get; init; }
    public required int Page1 { get; init; }
    public required int Index1 { get; init; }
    public required int XPost { get; init; }
    public required int YPost { get; init; }
}
