using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.Heartbeat, ExpectedSize = 45,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct HeartbeatRequest : IIncomingPacket<HeartbeatRequest>
{
    public required uint LastSend { get; init; }
    [FixedArray(32)] public required byte[] Data { get; init; }
}
