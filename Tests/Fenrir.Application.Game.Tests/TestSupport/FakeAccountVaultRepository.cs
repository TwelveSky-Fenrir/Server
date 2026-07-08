using Fenrir.Data.Abstractions.Accounts;
using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Application.Game.Tests.TestSupport;

/// <summary>In-memory stand-in for IAccountVaultRepository -- a single account's vault, no real persistence.</summary>
internal sealed class FakeAccountVaultRepository : IAccountVaultRepository
{
    private readonly Dictionary<int, (long Money, long Money2, Dictionary<short, AccountVaultItemSlotDto> Items)>
        _byAccount = new();

    public (int CharacterId, byte Container, IReadOnlyList<CharacterItemSlotTvp> Items, int AccountId,
        IReadOnlyList<AccountVaultItemSlotTvp> VaultItems)? LastItemTransfer { get; private set; }

    public (int CharacterId, long DeltaCharacterMoney, int AccountId, long DeltaVaultMoney)? LastMoneyTransfer
    {
        get;
        private set;
    }

    public Exception? ThrowOnMoneyTransfer { get; set; }
    public Exception? ThrowOnItemTransfer { get; set; }

    public ValueTask<(AccountVaultBalanceDto? Balance, IReadOnlyList<AccountVaultItemSlotDto> Items)> GetAsync(
        int accountId, CancellationToken ct)
    {
        if (!_byAccount.TryGetValue(accountId, out var vault))
            return ValueTask.FromResult<(AccountVaultBalanceDto?, IReadOnlyList<AccountVaultItemSlotDto>)>((null,
                []));

        var balance = new AccountVaultBalanceDto(accountId, vault.Money, vault.Money2, DateTime.UtcNow);
        return ValueTask.FromResult<(AccountVaultBalanceDto?, IReadOnlyList<AccountVaultItemSlotDto>)>((balance,
            vault.Items.Values.ToList()));
    }

    public ValueTask TransferMoneyWithCharacterAsync(int characterId, long deltaCharacterMoney, int accountId,
        long deltaVaultMoney, CancellationToken ct)
    {
        if (ThrowOnMoneyTransfer is { } ex)
            throw ex;

        LastMoneyTransfer = (characterId, deltaCharacterMoney, accountId, deltaVaultMoney);

        if (!_byAccount.TryGetValue(accountId, out var vault))
            vault = (0, 0, new Dictionary<short, AccountVaultItemSlotDto>());

        _byAccount[accountId] = (vault.Money + deltaVaultMoney, vault.Money2, vault.Items);
        return ValueTask.CompletedTask;
    }

    public ValueTask TransferItemWithCharacterAsync(int characterId, byte container,
        IReadOnlyList<CharacterItemSlotTvp> items, int accountId, IReadOnlyList<AccountVaultItemSlotTvp> vaultItems,
        CancellationToken ct)
    {
        if (ThrowOnItemTransfer is { } ex)
            throw ex;

        LastItemTransfer = (characterId, container, items, accountId, vaultItems);

        if (!_byAccount.TryGetValue(accountId, out var vault))
            vault = (0, 0, new Dictionary<short, AccountVaultItemSlotDto>());

        var slots = vaultItems.ToDictionary(i => i.SlotIndex,
            i => new AccountVaultItemSlotDto(i.SlotIndex, i.ItemId, i.Quantity, i.Value, i.SerialNumber,
                i.SocketData));
        _byAccount[accountId] = (vault.Money, vault.Money2, slots);
        return ValueTask.CompletedTask;
    }

    public ValueTask SetItemsAsync(int accountId, IReadOnlyList<AccountVaultItemSlotTvp> items, CancellationToken ct)
    {
        if (!_byAccount.TryGetValue(accountId, out var vault))
            vault = (0, 0, new Dictionary<short, AccountVaultItemSlotDto>());

        var slots = items.ToDictionary(i => i.SlotIndex,
            i => new AccountVaultItemSlotDto(i.SlotIndex, i.ItemId, i.Quantity, i.Value, i.SerialNumber,
                i.SocketData));
        _byAccount[accountId] = (vault.Money, vault.Money2, slots);
        return ValueTask.CompletedTask;
    }

    /// <summary>Seeds a vault balance/items for a test to assert against, bypassing the transfer methods.</summary>
    public void Seed(int accountId, long money, long money2,
        params (short SlotIndex, AccountVaultItemSlotDto Row)[] items)
    {
        var slots = items.ToDictionary(i => i.SlotIndex, i => i.Row);
        _byAccount[accountId] = (money, money2, slots);
    }
}
