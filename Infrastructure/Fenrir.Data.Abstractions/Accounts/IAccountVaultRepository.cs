using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Data.Abstractions.Accounts;

public interface IAccountVaultRepository
{
    public ValueTask<(AccountVaultBalanceDto? Balance, IReadOnlyList<AccountVaultItemSlotDto> Items)> GetAsync(
        int accountId, CancellationToken ct);

    /// <summary>
    ///     Optional trailing <c>audit*</c> parameters nest a SaveSlotMoney audit row into this same procedure call
    ///     (see usp_AccountVault_TransferMoneyWithCharacter.sql) instead of the caller making a second, unshared
    ///     round trip to <c>IEventLogRepository.LogAsync</c> afterward (transaction-composition-audit finding).
    ///     Omit <paramref name="auditEventCode" /> to skip logging.
    /// </summary>
    public ValueTask TransferMoneyWithCharacterAsync(int characterId, long deltaCharacterMoney, int accountId,
        long deltaVaultMoney, CancellationToken ct, short? auditEventCode = null, int? auditQuantity = null);

    public ValueTask TransferItemWithCharacterAsync(int characterId, byte container,
        IReadOnlyList<CharacterItemSlotTvp> items, int accountId,
        IReadOnlyList<AccountVaultItemSlotTvp> vaultItems, CancellationToken ct);

    public ValueTask SetItemsAsync(int accountId, IReadOnlyList<AccountVaultItemSlotTvp> items, CancellationToken ct);
}
