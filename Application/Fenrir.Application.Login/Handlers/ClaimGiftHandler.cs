using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Login;
using Fenrir.Data.Accounts;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Handlers;

/// <summary>
///     op21 CL_WANT_GIFT_SEND — GiftInfoIndex is a POSITION in GiftListHandler's oldest-first pending list, not a
///     database key.
/// </summary>
public sealed class ClaimGiftHandler(IGiftRepository gifts, ILogger<ClaimGiftHandler> logger)
    : IAsyncPacketHandler<ClaimGiftRequest>
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

        var pending = await gifts.GetPendingByAccountAsync(accountId, cancellationToken);
        if (packet.GiftInfoIndex >= pending.Count)
        {
            session.Send(new ClaimGiftResponse { Result = 1 });
            return;
        }

        try
        {
            await gifts.ClaimIntoVaultAsync(pending[packet.GiftInfoIndex].GiftId, accountId, cancellationToken);
        }
        catch (Exception ex)
        {
            // Broad catch by design: this project has no SqlClient dependency, so SQL error codes aren't inspected.
            logger.LogWarning(ex,
                "Account {AccountId} gift claim ClaimIntoVaultAsync failed (treated as vault full)", accountId);
            session.Send(new ClaimGiftResponse { Result = 2 });
            return;
        }

        session.Send(new ClaimGiftResponse { Result = 0 });
    }
}
