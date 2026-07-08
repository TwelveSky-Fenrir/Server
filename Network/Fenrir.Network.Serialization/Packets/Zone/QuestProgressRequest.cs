using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.QuestProgress, ExpectedSize = 29,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly record struct QuestProgressRequest : IIncomingPacket<QuestProgressRequest>
{
    public required int Sort { get; init; }
    public required int Page1 { get; init; }
    public required int Index1 { get; init; }
    public required int XPost { get; init; }
    public required int YPost { get; init; }
}
