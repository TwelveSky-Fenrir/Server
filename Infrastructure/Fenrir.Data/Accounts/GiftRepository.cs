using System.Collections.ObjectModel;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;

namespace Fenrir.Data.Accounts;

// Interface (unlike AccountRepository) so GiftListHandler/ClaimGiftHandler can be unit-tested without a SQL container.
public interface IGiftRepository
{
    public ValueTask<ReadOnlyCollection<PendingGiftDto>> GetPendingByAccountAsync(int accountId, CancellationToken ct);

    /// <summary>
    ///     Atomically claims the gift into the shared vault. Throws SQL 50220 (not found/not owned/claimed) or 50274
    ///     (vault full, 28 slots).
    /// </summary>
    public ValueTask<short> ClaimIntoVaultAsync(int giftId, int accountId, CancellationToken ct);
}

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
}
