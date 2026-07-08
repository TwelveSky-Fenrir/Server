using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;
using Fenrir.Network.Serialization.Zone.Wire;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.Heartbeat, ExpectedSize = 45,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct HeartbeatRequest : IIncomingPacket<HeartbeatRequest>
{
    public required uint LastSend { get; init; }
    [FixedArray(32)] public required byte[] Data { get; init; }
}
