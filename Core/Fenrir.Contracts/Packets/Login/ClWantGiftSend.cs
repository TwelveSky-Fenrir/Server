using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Login;

/// <summary>
///     CL_WANT_GIFT_SEND (CLIENT.h l.96-100): claim the gift at page <see cref="GiftInfoIndex" /> into the
///     shared account chest (login protocol report §4.21, GIFT_V2 off in EU33 so the V1 <c>uGiftInfo</c> path
///     applies). Same second-login gate as ops 17/18/19/22/25.
/// </summary>
[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.WantGiftSend,
    ExpectedSize = 17,
    AllowedStates = [(byte)LoginSessionState.Authenticated, (byte)LoginSessionState.CharSelect])]
public readonly partial record struct ClWantGiftSend : IIncomingPacket<ClWantGiftSend>
{
    /// <summary>Never read by the legacy handler (login protocol report §8) — carried for wire parity only.</summary>
    public required int Sort { get; init; }

    /// <summary>Gift page to claim, 0..9 (MAX_GIFT_ITEM_PAGE_NUM); out of range ⇒ legacy Quit().</summary>
    public required int GiftInfoIndex { get; init; }
}
