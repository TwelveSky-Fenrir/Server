using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Data.Abstractions.Commerce;

public interface ICashRepository
{
    public ValueTask<int> GetBalanceAsync(int accountId, CancellationToken ct);

    public ValueTask<int> DebitAndGrantItemAsync(int accountId, int amount, byte reason, int productId,
        int characterId, byte container, IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct);

    /// <summary>
    ///     game.usp_Cash_Credit: atomic balance increment + game.CashLog audit row (mints the account's
    ///     game.AccountCash row on a first credit). No upper bound is enforced here beyond the stored
    ///     procedure's own floor-of-zero-amount guard (throws SQL 50241 for a non-positive amount) -- callers
    ///     that need a per-feature ceiling must enforce it themselves before calling this.
    /// </summary>
    public ValueTask CreditAsync(int accountId, int amount, byte reason, int? productId, CancellationToken ct);
}
