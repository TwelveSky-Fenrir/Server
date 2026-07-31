using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.FishingProgress, ExpectedSize = 17,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct FishingProgressRequest : IIncomingPacket<FishingProgressRequest>
{
    public required int Sort { get; init; }
    public required int FishingStep { get; init; }
}
