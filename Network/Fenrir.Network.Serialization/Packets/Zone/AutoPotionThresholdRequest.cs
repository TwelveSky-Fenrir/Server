using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

// Either value outside 0..5 must Quit(). No ZC reply — silent handler.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.AutoPotionThreshold, ExpectedSize = 17,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct AutoPotionThresholdRequest : IIncomingPacket<AutoPotionThresholdRequest>
{
    public required int Value01 { get; init; }
    public required int Value02 { get; init; }
}
