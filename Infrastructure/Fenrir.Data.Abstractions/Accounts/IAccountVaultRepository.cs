using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Data.Abstractions.Accounts;

/// <summary>
///     game.AccountVault/AccountVaultItems access -- the account-scoped (not character-scoped) shared "Save"
///     vault/bank the legacy models as masterinfo.uSaveMoney/uSaveItem. Every character on the same account
///     shares one vault; callers must not cache its contents across requests the way
///     <c>PlayerRuntimeState</c> caches character-scoped state, since a different character on the same
///     account could be transferring concurrently on another shard -- always re-fetch immediately before
///     computing a transfer.
/// </summary>
public interface IAccountVaultRepository
{
    /// <summary>
    ///     Current balance/contents. A never-initialized vault (no row yet) returns <see langword="null" /> for
    ///     <c>Balance</c> and an empty <c>Items</c> list -- callers should treat that the same as an
    ///     all-zero/all-empty vault; <see cref="TransferMoneyWithCharacterAsync" />/
    ///     <see cref="TransferItemWithCharacterAsync" /> auto-create the row on first write.
    /// </summary>
    public ValueTask<(AccountVaultBalanceDto? Balance, IReadOnlyList<AccountVaultItemSlotDto> Items)> GetAsync(
        int accountId, CancellationToken ct);

    /// <summary>
    ///     Atomic transfer between a character's wallet (game.Characters.Money) and its account's shared vault
    ///     (game.AccountVault.Money) -- CZ_PROCESS_DATA_SEND tSort 231 (deposit)/232 (withdraw). Throws SQL
    ///     50338 (insufficient/unknown wallet) or 50339 (insufficient vault balance / would exceed cap).
    /// </summary>
    public ValueTask TransferMoneyWithCharacterAsync(int characterId, long deltaCharacterMoney, int accountId,
        long deltaVaultMoney, CancellationToken ct);

    /// <summary>
    ///     Atomic transfer between one character inventory container (whole-container replace) and the
    ///     account's shared vault items (whole-list replace) -- CZ_PROCESS_DATA_SEND tSort 228/251
    ///     (deposit)/229/249 (withdraw). Empty-list omission follows the same rule as
    ///     <see cref="ICharacterRepository.ReplaceContainerAsync" />.
    /// </summary>
    public ValueTask TransferItemWithCharacterAsync(int characterId, byte container,
        IReadOnlyList<CharacterItemSlotTvp> items, int accountId,
        IReadOnlyList<AccountVaultItemSlotTvp> vaultItems, CancellationToken ct);

    /// <summary>
    ///     Whole-list replace of the vault's own items only -- CZ_PROCESS_DATA_SEND tSort 230 (bank-to-bank
    ///     rearrange), which never touches any character container. Does not auto-create the vault row (a
    ///     rearrange can only ever be requested once a deposit has already created one).
    /// </summary>
    public ValueTask SetItemsAsync(int accountId, IReadOnlyList<AccountVaultItemSlotTvp> items, CancellationToken ct);
}
