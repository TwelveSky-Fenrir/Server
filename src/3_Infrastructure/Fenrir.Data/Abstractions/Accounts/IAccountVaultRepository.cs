using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Data.Abstractions.Accounts;

public interface IAccountVaultRepository
{
    public ValueTask<(AccountVaultBalanceDto? Balance, IReadOnlyList<AccountVaultItemSlotDto> Items)> GetAsync(
        int accountId, CancellationToken ct);

        public ValueTask TransferMoneyWithCharacterAsync(int characterId, long deltaCharacterMoney, int accountId,
        long deltaVaultMoney, CancellationToken ct, short? auditEventCode = null, int? auditQuantity = null);

    public ValueTask TransferItemWithCharacterAsync(int characterId, byte container,
        IReadOnlyList<CharacterItemSlotTvp> items, int accountId,
        IReadOnlyList<AccountVaultItemSlotTvp> vaultItems, CancellationToken ct);

    public ValueTask SetItemsAsync(int accountId, IReadOnlyList<AccountVaultItemSlotTvp> items, CancellationToken ct);
}
