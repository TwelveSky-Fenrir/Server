using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;
using Fenrir.Network.Serialization.Zone.Wire;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

// Ignored silently unless the player is in the fishing "catch" state; empty payload.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.FishingCatch, ExpectedSize = 9,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct FishingCatchRequest : IIncomingPacket<FishingCatchRequest>
{
}
