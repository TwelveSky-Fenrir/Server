using System.Collections.ObjectModel;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Accounts;

namespace Fenrir.Data.Accounts;

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

        public async ValueTask<short> ClaimIntoVaultAsync(int giftId, int accountId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Gift_ClaimIntoVault", 1)
            .AddParameter("GiftId", giftId, SqlDbType.Int)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .Build();

        var result = await Db.FirstQueryAsync<GiftClaimResultDto>(sp, ct);
        return result!.SlotIndex;
    }

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
