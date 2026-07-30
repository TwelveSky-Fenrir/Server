using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.AutoPotionThreshold, ExpectedSize = 17,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct AutoPotionThresholdRequest : IIncomingPacket<AutoPotionThresholdRequest>
{
    public required int Value01 { get; init; }
    public required int Value02 { get; init; }
}
