using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Login;
using Fenrir.Data.Accounts;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Login.Handlers;

/// <summary>
///     op21 CL_WANT_GIFT_SEND — claim a gift page into the shared account vault (login protocol report
///     §4.21, "chantier V8"). Now wired to the REAL delivery queue: <see cref="GiftInfoIndex" /> is
///     resolved against the SAME oldest-first pending list <c>GiftListHandler</c> builds (the legacy's own
///     <c>uGiftInfo</c> array convention -- an index is a POSITION in that list, not a database key), then
///     claimed atomically (<c>usp_Gift_ClaimIntoVault</c>, D7 regime (b): Gifts.Status flip + AccountVaultItems
///     insert commit together).
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
            // Most likely the account vault is full (usp_Gift_ClaimIntoVault SQL 50274) -- a genuine race
            // where a concurrent claim already consumed this exact gift (SQL 50220) is rare and, either
            // way, this layer's established convention is a broad catch, never SQL-error-number
            // inspection (no Microsoft.Data.SqlClient dependency exists in this project, by design).
            logger.LogWarning(ex,
                "Account {AccountId} gift claim ClaimIntoVaultAsync failed (treated as vault full)", accountId);
            session.Send(new ClaimGiftResponse { Result = 2 });
            return;
        }

        session.Send(new ClaimGiftResponse { Result = 0 });
    }
}
