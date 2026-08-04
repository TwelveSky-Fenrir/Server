using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Accounts;

namespace Fenrir.Data.Accounts;

public sealed record AccountVaultRepository(ICaeriusNetDbContext Db) : IAccountVaultRepository
{
    public async ValueTask<(AccountVaultBalanceDto? Balance, IReadOnlyList<AccountVaultItemSlotV2Dto> Items)>
        GetAsync(int accountId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_AccountVault_Get", 32)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .Build();

        var (balances, items) =
            await Db.QueryMultipleReadOnlyCollectionAsync<AccountVaultBalanceDto, AccountVaultItemSlotV2Dto>(sp, ct);

        return (balances.Count > 0 ? balances[0] : null, items);
    }

    public async ValueTask TransferMoneyWithCharacterAsync(int characterId, long deltaCharacterMoney, int accountId,
        long deltaVaultMoney, CancellationToken ct, short? auditEventCode = null, int? auditQuantity = null)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_AccountVault_TransferMoneyWithCharacter", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("DeltaCharacterMoney", deltaCharacterMoney, SqlDbType.BigInt)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .AddParameter("DeltaVaultMoney", deltaVaultMoney, SqlDbType.BigInt)
            .AddParameter("AuditEventCode", (object?)auditEventCode ?? DBNull.Value, SqlDbType.SmallInt)
            .AddParameter("AuditQuantity", (object?)auditQuantity ?? DBNull.Value, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public ValueTask<bool> TryTransferItemWithCharacterAsync(int accountId, int characterId, byte container,
        long expectedVaultRevision, AccountVaultCharacterSlotMutation characterSlot,
        AccountVaultItemSlotMutation vaultSlot, CancellationToken ct)
    {
        return TryApplyItemMutationAsync(accountId, characterId, container, expectedVaultRevision, characterSlot,
            vaultSlot, null, ct);
    }

    public ValueTask<bool> TryRearrangeItemsAsync(int accountId, long expectedVaultRevision,
        AccountVaultItemSlotMutation firstVaultSlot, AccountVaultItemSlotMutation secondVaultSlot,
        CancellationToken ct)
    {
        return TryApplyItemMutationAsync(accountId, null, null, expectedVaultRevision, null, firstVaultSlot,
            secondVaultSlot, ct);
    }

    private async ValueTask<bool> TryApplyItemMutationAsync(int accountId, int? characterId, byte? container,
        long expectedVaultRevision, AccountVaultCharacterSlotMutation? characterSlot,
        AccountVaultItemSlotMutation firstVaultSlot, AccountVaultItemSlotMutation? secondVaultSlot,
        CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(expectedVaultRevision);

        var builder = new StoredProcedureParametersBuilder("game", "usp_AccountVault_TransferItemWithCharacter", 1)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .AddParameter("ExpectedVaultRevision", expectedVaultRevision, SqlDbType.BigInt)
            .AddParameter("CharacterId", (object?)characterId ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("Container", (object?)container ?? DBNull.Value, SqlDbType.TinyInt);

        AddCharacterSlotMutation(builder, characterSlot);
        AddVaultSlotMutation(builder, "Vault1", firstVaultSlot);
        AddVaultSlotMutation(builder, "Vault2", secondVaultSlot);

        return await Db.ExecuteScalarAsync<bool>(builder.Build(), ct);
    }

    private static void AddCharacterSlotMutation(StoredProcedureParametersBuilder builder,
        AccountVaultCharacterSlotMutation? mutation)
    {
        var expected = mutation?.Expected;
        var replacement = mutation?.Replacement;

        builder
            .AddParameter("CharacterSlot", (object?)mutation?.Slot ?? DBNull.Value, SqlDbType.TinyInt)
            .AddParameter("ExpectedCharacterItemId", (object?)expected?.ItemId ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("ExpectedCharacterQuantity", expected?.Quantity ?? 0, SqlDbType.Int)
            .AddParameter("ExpectedCharacterEnchant", expected?.Enchant ?? 0, SqlDbType.TinyInt)
            .AddParameter("ExpectedCharacterCombine", expected?.Combine ?? 0, SqlDbType.TinyInt)
            .AddParameter("ExpectedCharacterRefine", expected?.Refine ?? 0, SqlDbType.TinyInt)
            .AddParameter("ExpectedCharacterSocket", expected?.Socket ?? 0, SqlDbType.TinyInt)
            .AddParameter("ExpectedCharacterSocketGem1", expected?.SocketGem1 ?? 0, SqlDbType.Int)
            .AddParameter("ExpectedCharacterSocketGem2", expected?.SocketGem2 ?? 0, SqlDbType.Int)
            .AddParameter("ExpectedCharacterSocketGem3", expected?.SocketGem3 ?? 0, SqlDbType.Int)
            .AddParameter("ExpectedCharacterExpireDate", expected?.ExpireDate ?? 0, SqlDbType.Int)
            .AddParameter("ExpectedCharacterSerial", expected?.Serial ?? 0, SqlDbType.Int)
            .AddParameter("ExpectedCharacterXPos", expected?.XPos ?? 0, SqlDbType.TinyInt)
            .AddParameter("ExpectedCharacterYPos", expected?.YPos ?? 0, SqlDbType.TinyInt)
            .AddParameter("NewCharacterItemId", (object?)replacement?.ItemId ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("NewCharacterQuantity", replacement?.Quantity ?? 0, SqlDbType.Int)
            .AddParameter("NewCharacterEnchant", replacement?.Enchant ?? 0, SqlDbType.TinyInt)
            .AddParameter("NewCharacterCombine", replacement?.Combine ?? 0, SqlDbType.TinyInt)
            .AddParameter("NewCharacterRefine", replacement?.Refine ?? 0, SqlDbType.TinyInt)
            .AddParameter("NewCharacterSocket", replacement?.Socket ?? 0, SqlDbType.TinyInt)
            .AddParameter("NewCharacterSocketGem1", replacement?.SocketGem1 ?? 0, SqlDbType.Int)
            .AddParameter("NewCharacterSocketGem2", replacement?.SocketGem2 ?? 0, SqlDbType.Int)
            .AddParameter("NewCharacterSocketGem3", replacement?.SocketGem3 ?? 0, SqlDbType.Int)
            .AddParameter("NewCharacterExpireDate", replacement?.ExpireDate ?? 0, SqlDbType.Int)
            .AddParameter("NewCharacterSerial", replacement?.Serial ?? 0, SqlDbType.Int)
            .AddParameter("NewCharacterXPos", replacement?.XPos ?? 0, SqlDbType.TinyInt)
            .AddParameter("NewCharacterYPos", replacement?.YPos ?? 0, SqlDbType.TinyInt);
    }

    private static void AddVaultSlotMutation(StoredProcedureParametersBuilder builder, string prefix,
        AccountVaultItemSlotMutation? mutation)
    {
        var expected = mutation?.Expected;
        var replacement = mutation?.Replacement;

        builder
            .AddParameter($"{prefix}Slot", (object?)mutation?.SlotIndex ?? DBNull.Value, SqlDbType.SmallInt)
            .AddParameter($"Expected{prefix}ItemId", (object?)expected?.ItemId ?? DBNull.Value, SqlDbType.Int)
            .AddParameter($"Expected{prefix}Quantity", expected?.Quantity ?? 0, SqlDbType.Int)
            .AddParameter($"Expected{prefix}SerialNumber", expected?.SerialNumber ?? 0, SqlDbType.Int)
            .AddParameter($"New{prefix}ItemId", (object?)replacement?.ItemId ?? DBNull.Value, SqlDbType.Int)
            .AddParameter($"New{prefix}Quantity", replacement?.Quantity ?? 0, SqlDbType.Int)
            .AddParameter($"New{prefix}Value", replacement?.Value ?? 0, SqlDbType.Int)
            .AddParameter($"New{prefix}SerialNumber", replacement?.SerialNumber ?? 0, SqlDbType.Int)
            .AddParameter($"New{prefix}SocketData", (object?)replacement?.SocketData ?? DBNull.Value,
                SqlDbType.NVarChar)
            .AddParameter($"New{prefix}SocketGem1", replacement?.SocketGem1 ?? 0, SqlDbType.Int)
            .AddParameter($"New{prefix}SocketGem2", replacement?.SocketGem2 ?? 0, SqlDbType.Int)
            .AddParameter($"New{prefix}SocketGem3", replacement?.SocketGem3 ?? 0, SqlDbType.Int)
            .AddParameter($"New{prefix}ExpireDate", replacement?.ExpireDate ?? 0, SqlDbType.Int);
    }
}
