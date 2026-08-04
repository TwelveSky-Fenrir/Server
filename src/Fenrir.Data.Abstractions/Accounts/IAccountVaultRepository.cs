namespace Fenrir.Data.Abstractions.Accounts;

public interface IAccountVaultRepository
{
    public ValueTask<(AccountVaultBalanceDto? Balance, IReadOnlyList<AccountVaultItemSlotV2Dto> Items)> GetAsync(
        int accountId, CancellationToken ct);

    public ValueTask TransferMoneyWithCharacterAsync(int characterId, long deltaCharacterMoney, int accountId,
        long deltaVaultMoney, CancellationToken ct, short? auditEventCode = null, int? auditQuantity = null);

    public ValueTask<bool> TryTransferItemWithCharacterAsync(int accountId, int characterId, byte container,
        long expectedVaultRevision, AccountVaultCharacterSlotMutation characterSlot,
        AccountVaultItemSlotMutation vaultSlot, CancellationToken ct);

    public ValueTask<bool> TryRearrangeItemsAsync(int accountId, long expectedVaultRevision,
        AccountVaultItemSlotMutation firstVaultSlot, AccountVaultItemSlotMutation secondVaultSlot,
        CancellationToken ct);
}
