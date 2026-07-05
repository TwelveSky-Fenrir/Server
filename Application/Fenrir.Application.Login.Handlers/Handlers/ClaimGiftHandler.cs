using Fenrir.Application.Login.Abstractions.ClaimGift;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Login;

namespace Fenrir.Application.Login.Handlers.Handlers;

/// <summary>
///     op21 CL_WANT_GIFT_SEND — GiftInfoIndex is a POSITION in GiftListHandler's oldest-first pending list, not a
///     database key.
/// </summary>
public sealed class ClaimGiftHandler(IClaimGiftService claimGiftService) : IAsyncPacketHandler<ClaimGiftRequest>
{
    private const int MaxGiftPageIndex = 9; // MAX_GIFT_ITEM_PAGE_NUM - 1

    public async ValueTask HandleAsync(ClaimGiftRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;
        var accountId = loginSession.AccountId!.Value;

        if (packet.GiftInfoIndex is < 0 or > MaxGiftPageIndex)
        {
            loginSession.Abort(DisconnectReason.Malformed);
            return;
        }

        var result = await claimGiftService.ClaimGiftAsync(accountId, packet.GiftInfoIndex, cancellationToken);

        session.Send(new ClaimGiftResponse
        {
            Result = result.Outcome switch
            {
                ClaimGiftOutcome.Success => 0,
                ClaimGiftOutcome.IndexNotPending => 1,
                _ => 2
            }
        });
    }
}
