using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Characters;

namespace Fenrir.Data.Commerce;

// game.AccountCash/CashLog access (real-money cash-shop). Combined debit+grant is atomic: a purchase must never take money without durably granting the item.
public sealed record CashRepository(ICaeriusNetDbContext Db) : ICashRepository
{
    public async ValueTask<int> GetBalanceAsync(int accountId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Cash_GetBalance", 1)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .Build();

        return await Db.ExecuteScalarAsync<int>(sp, ct);
    }

    /// <summary>Atomic cash debit + one container replace. Returns the post-debit balance. Throws SQL 50241 (non-positive amount) or 50240 (insufficient balance).</summary>
    public async ValueTask<int> DebitAndGrantItemAsync(int accountId, int amount, byte reason, int productId,
        int characterId, byte container, IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct)
    {
        var builder = new StoredProcedureParametersBuilder("game", "usp_Cash_DebitAndGrantItem", 1)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .AddParameter("Amount", amount, SqlDbType.Int)
            .AddParameter("Reason", reason, SqlDbType.TinyInt)
            .AddParameter("ProductId", productId, SqlDbType.Int)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("Container", container, SqlDbType.TinyInt);

        if (items.Count > 0) builder.AddTvpParameter("Items", items);

        return await Db.ExecuteScalarAsync<int>(builder.Build(), ct);
    }
}
