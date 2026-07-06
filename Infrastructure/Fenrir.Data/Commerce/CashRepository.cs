using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Commerce;

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

    /// <summary>
    ///     Atomic cash debit + one container replace. Returns the post-debit balance. Throws SQL 50241 (non-positive
    ///     amount) or 50240 (insufficient balance).
    /// </summary>
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

    /// <summary>
    ///     Atomic cash credit (balance increment + game.CashLog audit row); throws SQL 50241 for a
    ///     non-positive amount. No result set is returned by usp_Cash_Credit -- callers that need the
    ///     post-credit balance should follow up with <see cref="GetBalanceAsync" />.
    /// </summary>
    public async ValueTask CreditAsync(int accountId, int amount, byte reason, int? productId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Cash_Credit", 0)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .AddParameter("Amount", amount, SqlDbType.Int)
            .AddParameter("Reason", reason, SqlDbType.TinyInt)
            .AddParameter("ProductId", (object?)productId ?? DBNull.Value, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }
}
