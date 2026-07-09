using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;
using Fenrir.Network.Serialization.Zone.Wire;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

// Sort: 1=select slot (Value=0-9), 2=no-op, 3=equip, 4=remove, 5=return to inventory; other disconnects.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.StellarCoreState, ExpectedSize = 17,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly record struct StellarCoreStateRequest : IIncomingPacket<StellarCoreStateRequest>
{
    public required int Sort { get; init; }
    public required int Value { get; init; }
}
