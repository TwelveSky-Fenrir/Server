using System.Collections.ObjectModel;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Accounts;

namespace Fenrir.Data.Accounts;

// Facade over game.usp_Gift_*. The delivery path that mints a game.Gifts row (purchase, GM grant) is out of scope -- this only backs the LoginServer list/claim flow.
public sealed record GiftRepository(ICaeriusNetDbContext Db) : IGiftRepository
{
    public async ValueTask<ReadOnlyCollection<PendingGiftDto>> GetPendingByAccountAsync(int accountId,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Gift_GetPendingByAccount", 10)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .Build();

        return await Db.QueryAsReadOnlyCollectionAsync<PendingGiftDto>(sp, ct);
    }

    /// <summary>
    ///     Atomically claims the gift into the shared vault. Throws SQL 50220 (not found/not owned/claimed) or 50274
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

    /// <summary>
    ///     Mints one pending gift for a single named account. See <see cref="IGiftRepository.EnqueueAsync" /> for
    ///     why this deliberately has no broadcast mode and no validation of <paramref name="productId" />,
    ///     <paramref name="quantity" />, or <paramref name="value" />.
    /// </summary>
    public async ValueTask<int> EnqueueAsync(int accountId, int? productId, int quantity, int value,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Gift_Enqueue", 1)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .AddParameter("ProductId", (object?)productId ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("Quantity", quantity, SqlDbType.Int)
            .AddParameter("Value", value, SqlDbType.Int)
            .Build();

        return await Db.ExecuteScalarAsync<int>(sp, ct);
    }
}
