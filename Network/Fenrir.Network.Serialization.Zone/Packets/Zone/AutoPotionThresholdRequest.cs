using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;
using Fenrir.Network.Serialization.Zone.Wire;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

// Either value outside 0..5 must Quit(). No ZC reply — silent handler.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.AutoPotionThreshold, ExpectedSize = 17,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly record struct AutoPotionThresholdRequest : IIncomingPacket<AutoPotionThresholdRequest>
{
    public required int Value01 { get; init; }
    public required int Value02 { get; init; }
}
