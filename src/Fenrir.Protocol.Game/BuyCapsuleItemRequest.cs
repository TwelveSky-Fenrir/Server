using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.CapsuleItemBuy, ExpectedSize = 21,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct BuyCapsuleItemRequest : IIncomingPacket<BuyCapsuleItemRequest>
{
    public required int Sort { get; init; }
    public required int Value01 { get; init; }
    public required int Value02 { get; init; }
}
