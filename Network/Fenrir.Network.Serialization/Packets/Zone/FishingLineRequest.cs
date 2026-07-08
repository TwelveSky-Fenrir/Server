using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

// Fishing zone (52) only. LocationX/LocationZ are dead fields (never read, but still part of the wire layout). Sort: 1=cast, 2=reel; else Quit().
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.FishingLine, ExpectedSize = 21,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct FishingLineRequest : IIncomingPacket<FishingLineRequest>
{
    public required int Sort { get; init; }
    public required float LocationX { get; init; }
    public required float LocationZ { get; init; }
}
