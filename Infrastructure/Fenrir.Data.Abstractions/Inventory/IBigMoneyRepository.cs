namespace Fenrir.Data.Abstractions.Inventory;

/// <summary>
///     Atomic BigMoney ("1B"/"1 Md") counter transfers for CZ_PROCESS_DATA_SEND tSort 241/244 (Inventory
///     &lt;-&gt; Store) and 242/245 (Inventory &lt;-&gt; Save/vault). A brand-new dedicated interface rather
///     than additions to <see cref="ICharacterRepository" />/
///     <see cref="Fenrir.Data.Abstractions.Accounts.IAccountVaultRepository" /> -- see the C8 (wave7) wiring
///     manifest for why this pass could not edit either already-large interface directly (concurrent-agent
///     constraint). Every method here is a dumb, server-side-bound-checked delta-adjust primitive (same
///     "guarded UPDATE, THROW on violation" shape as <c>ICharacterRepository.AdjustStoreMoneyAsync</c>) --
///     callers never pre-fetch a balance; a THROW on an insufficient/over-cap adjustment is the caller's only
///     signal, translated to a hard disconnect by <c>BigMoneyTransferService</c>, matching the C8 contract's
///     own Error/failure semantics for this whole family.
/// </summary>
public interface IBigMoneyRepository
{
    /// <summary>
    ///     tSort 241 (deposit, <paramref name="deltaInventoryBigMoney" /> negative/<paramref name="deltaStoreBigMoney" />
    ///     positive) / 244 (withdraw, the reverse) -- both counters live on the same game.Characters row.
    /// </summary>
    public ValueTask AdjustInventoryStoreAsync(int characterId, int deltaInventoryBigMoney,
        int deltaStoreBigMoney, CancellationToken ct);

    /// <summary>
    ///     tSort 242 (deposit, <paramref name="deltaInventoryBigMoney" /> negative/<paramref name="deltaVaultBigMoney" />
    ///     positive) / 245 (withdraw, the reverse) -- <paramref name="deltaInventoryBigMoney" /> touches
    ///     game.Characters.BigMoney, <paramref name="deltaVaultBigMoney" /> touches the account-scoped
    ///     game.AccountVault.BigMoney, atomically in one transaction (auto-creates the vault row on first use).
    /// </summary>
    public ValueTask AdjustInventorySaveAsync(int characterId, int deltaInventoryBigMoney, int accountId,
        int deltaVaultBigMoney, CancellationToken ct);
}
