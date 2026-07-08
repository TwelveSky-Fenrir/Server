using Fenrir.Application.Login.Abstractions.ClaimGift;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Login;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Handlers.Handlers;

/// <summary>
///     op21 CL_WANT_GIFT_SEND — GiftInfoIndex is a POSITION in GiftListHandler's oldest-first pending list, not a
///     database key.
/// </summary>
public sealed class ClaimGiftHandler(IClaimGiftService claimGiftService, ILogger<ClaimGiftHandler> logger)
    : IAsyncPacketHandler<ClaimGiftRequest>
{
    private const int MaxGiftPageIndex = 9; // MAX_GIFT_ITEM_PAGE_NUM - 1

    public async ValueTask HandleAsync(ClaimGiftRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var loginSession = (LoginClientSession)session;
        var accountId = loginSession.AccountId!.Value;

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug(
                "Session {SessionId}: op21 CL_WANT_GIFT_SEND received for account {AccountId}, index {GiftInfoIndex}",
                session.SessionId, accountId, packet.GiftInfoIndex);

        if (packet.GiftInfoIndex is < 0 or > MaxGiftPageIndex)
        {
            logger.LogWarning(
                "Gift claim rejected: account {AccountId} sent out-of-range index {GiftInfoIndex} -- aborting",
                accountId, packet.GiftInfoIndex);
            loginSession.Abort(DisconnectReason.Malformed);
            return;
        }

        var result = await claimGiftService.ClaimGiftAsync(accountId, packet.GiftInfoIndex, cancellationToken);

        switch (result.Outcome)
        {
            case ClaimGiftOutcome.Success:
                logger.LogInformation("Gift claimed: account {AccountId} index {GiftInfoIndex}", accountId,
                    packet.GiftInfoIndex);
                break;
            case ClaimGiftOutcome.IndexNotPending:
                logger.LogWarning(
                    "Gift claim rejected: account {AccountId} index {GiftInfoIndex} is not a pending gift",
                    accountId, packet.GiftInfoIndex);
                break;
            default:
                // ClaimFailed: the exception itself is already logged at ClaimGiftService; this is the summary.
                logger.LogWarning("Gift claim failed for account {AccountId} index {GiftInfoIndex}", accountId,
                    packet.GiftInfoIndex);
                break;
        }

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
