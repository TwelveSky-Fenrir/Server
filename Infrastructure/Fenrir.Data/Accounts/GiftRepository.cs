using System.Collections.ObjectModel;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;

namespace Fenrir.Data.Accounts;

/// <summary>
///     game.Gifts/GiftLog surface (login protocol report §4.21/§4.25, "chantier V8"). An interface --
///     unlike the concrete-only <c>Fenrir.Data.Accounts.AccountRepository</c> -- because
///     <c>GiftListHandler</c>/<c>ClaimGiftHandler</c> are unit-tested in Fenrir.Application.Login.Tests
///     without a SQL container, same rationale as <see cref="IAccountPinRepository" />.
/// </summary>
public interface IGiftRepository
{
    ValueTask<ReadOnlyCollection<PendingGiftDto>> GetPendingByAccountAsync(int accountId, CancellationToken ct);

    /// <summary>
    ///     Atomically claims the gift and places its item into the account's shared vault
    ///     (usp_Gift_ClaimIntoVault). Throws SQL 50220 (not found/not owned/already claimed) or 50274
    ///     (vault full, 28 slots).
    /// </summary>
    ValueTask<short> ClaimIntoVaultAsync(int giftId, int accountId, CancellationToken ct);
}

/// <summary>
///     Singleton facade over game.usp_Gift_* -- same shape as <c>AccountRepository</c>: no SqlDbType/builder
///     ever leaks past this type. The web-API delivery path itself (whatever mints a game.Gifts row -- a
///     cash-shop purchase, a GM grant) is out of THIS repository's scope (usp_Gift_Enqueue already exists,
///     unconsumed by any Fenrir handler yet -- see V8 StructuredOutput open issues); this type only backs
///     the LoginServer-side list/claim flow.
/// </summary>
public sealed record GiftRepository(ICaeriusNetDbContext Db) : IGiftRepository
{
    public async ValueTask<ReadOnlyCollection<PendingGiftDto>> GetPendingByAccountAsync(int accountId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Gift_GetPendingByAccount", 10)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .Build();

        return await Db.QueryAsReadOnlyCollectionAsync<PendingGiftDto>(sp, ct);
    }

    /// <summary>
    ///     Atomically claims the gift and places its item into the account's shared vault
    ///     (usp_Gift_ClaimIntoVault). Throws SQL 50220 (not found/not owned/already claimed) or 50274
    ///     (vault full, 28 slots).
    /// </summary>
    public async ValueTask<short> ClaimIntoVaultAsync(int giftId, int accountId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Gift_ClaimIntoVault", 1)
            .AddParameter("GiftId", giftId, SqlDbType.Int)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .Build();

        var result = await Db.FirstQueryAsync<GiftClaimResultDto>(sp, ct);
        return result!.SlotIndex;
    }
}
