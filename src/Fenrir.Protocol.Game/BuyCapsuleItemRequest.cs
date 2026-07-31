using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

// Layout CZ_CAPSULE_ITEM_BUY_SEND Server/Header/Protocol/CLIENT.h:441-446 ; mort en M33 : opcode non enregistre dans W_FUNCTION.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.CapsuleItemBuy, ExpectedSize = 21,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct BuyCapsuleItemRequest : IIncomingPacket<BuyCapsuleItemRequest>
{
    public required int Sort { get; init; }
    public required int Value01 { get; init; }
    public required int Value02 { get; init; }
}
