using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;
using Fenrir.Network.Serialization.Zone.Wire;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.ZoneReady,
    ExpectedSize = 25,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct ZoneReadyRequest : IIncomingPacket<ZoneReadyRequest>
{
    public required int Tribe { get; init; }
    public required int AutoTime { get; init; }
    public required int AutoTime2 { get; init; }
    public required int AutoState { get; init; }
}
