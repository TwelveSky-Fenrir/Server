using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Characters;

namespace Fenrir.Data.Commerce;

/// <summary>
///     game.AccountCash/CashLog access (V8 Player Commerce &amp; Cash, contracts/04_commerce.md CZ
///     41/42/91 -- the real-money cash-shop). Singleton, injected only with
///     <see cref="ICaeriusNetDbContext" />. usp_Cash_GetBalance/Credit/Debit (account-only, no character)
///     already existed from phase A3 and are reused as-is here for the balance read; the combined
///     debit+grant write is new (D7 regime (b): a cash-shop purchase must never take an account's money
///     without also durably granting the item).
/// </summary>
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
    ///     Atomic cash debit + ONE character item container replace (usp_Cash_DebitAndGrantItem). Returns
    ///     the post-debit balance. Throws SQL 50241 (non-positive amount) or 50240 (insufficient balance).
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
}
