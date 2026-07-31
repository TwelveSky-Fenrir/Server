using Fenrir.Application.Login.Abstractions.ClaimGift;
using Fenrir.Application.Login.Sessions;
using Fenrir.Protocol.Login;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Handlers.Handlers;

public sealed class ClaimGiftHandler(IClaimGiftService claimGiftService, ILogger<ClaimGiftHandler> logger)
    : IAsyncPacketHandler<ClaimGiftRequest>
{
    private const int MaxGiftPageIndex = 9;

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
            case ClaimGiftOutcome.GiftUnavailable:
            case ClaimGiftOutcome.VaultFull:
            case ClaimGiftOutcome.PersistenceFailure:
                logger.LogWarning("Gift claim failed for account {AccountId} index {GiftInfoIndex} ({Outcome})",
                    accountId, packet.GiftInfoIndex, result.Outcome);
                break;
        }

        session.Send(new ClaimGiftResponse
        {
            Result = result.Outcome switch
            {
                ClaimGiftOutcome.Success => 0,
                ClaimGiftOutcome.GiftUnavailable => 1,
                ClaimGiftOutcome.VaultFull => 2,
                ClaimGiftOutcome.PersistenceFailure => 101,
                _ => throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, null)
            }
        });
    }
}
