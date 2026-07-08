using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

// Sort: 1=poll bite (needs step 3 + >=1min since cast; 10% catch/90% miss), 2=recast, 3=force FishingStep (clamped 0..5); else Quit(). FishingStep only read when Sort=3.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.FishingProgress, ExpectedSize = 17,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly record struct FishingProgressRequest : IIncomingPacket<FishingProgressRequest>
{
    public required int Sort { get; init; }
    public required int FishingStep { get; init; }
}
