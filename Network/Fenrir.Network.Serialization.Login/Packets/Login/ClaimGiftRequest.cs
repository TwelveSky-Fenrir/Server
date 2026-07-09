using Fenrir.Network.Serialization.Login.Wire;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Login.Packets.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.ClaimGift,
    ExpectedSize = 17,
    AllowedStates = [(byte)LoginSessionState.Authenticated, (byte)LoginSessionState.CharSelect])]
public readonly record struct ClaimGiftRequest : IIncomingPacket<ClaimGiftRequest>
{
    // Unused by the legacy handler; carried for wire parity only.
    public required int Sort { get; init; }

    // 0..9 (MAX_GIFT_ITEM_PAGE_NUM); out of range disconnects.
    public required int GiftInfoIndex { get; init; }
}
