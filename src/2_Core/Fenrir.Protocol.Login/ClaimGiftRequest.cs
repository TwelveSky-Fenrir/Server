using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.ClaimGift,
    ExpectedSize = 17,
    AllowedStates = [(byte)LoginSessionState.Authenticated, (byte)LoginSessionState.CharSelect])]
public readonly partial record struct ClaimGiftRequest : IIncomingPacket<ClaimGiftRequest>
{
    public required int Sort { get; init; }

    public required int GiftInfoIndex { get; init; }
}
