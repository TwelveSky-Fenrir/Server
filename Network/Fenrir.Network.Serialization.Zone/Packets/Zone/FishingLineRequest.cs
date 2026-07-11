using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;
using Fenrir.Network.Serialization.Zone.Wire;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.FishingLine, ExpectedSize = 21,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct FishingLineRequest : IIncomingPacket<FishingLineRequest>
{
    public required int Sort { get; init; }
    public required float LocationX { get; init; }
    public required float LocationZ { get; init; }
}
