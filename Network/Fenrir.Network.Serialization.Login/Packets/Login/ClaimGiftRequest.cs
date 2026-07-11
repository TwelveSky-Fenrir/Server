using Fenrir.Network.Serialization.Login.Wire;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Login.Packets.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.ClaimGift,
    ExpectedSize = 17,
    AllowedStates = [(byte)LoginSessionState.Authenticated, (byte)LoginSessionState.CharSelect])]
public readonly partial record struct ClaimGiftRequest : IIncomingPacket<ClaimGiftRequest>
{
    public required int Sort { get; init; }

    public required int GiftInfoIndex { get; init; }
}
