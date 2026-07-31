using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Accounts;
using Fenrir.Data.Abstractions.Characters;

namespace Fenrir.Data.Accounts;

public sealed record AccountVaultRepository(ICaeriusNetDbContext Db) : IAccountVaultRepository
{
    public async ValueTask<(AccountVaultBalanceDto? Balance, IReadOnlyList<AccountVaultItemSlotDto> Items)>
        GetAsync(int accountId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_AccountVault_Get", 32)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .Build();

        var (balances, items) =
            await Db.QueryMultipleReadOnlyCollectionAsync<AccountVaultBalanceDto, AccountVaultItemSlotDto>(sp, ct);

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

    public async ValueTask TransferItemWithCharacterAsync(int characterId, byte container,
        IReadOnlyList<CharacterItemSlotTvp> items, int accountId, IReadOnlyList<AccountVaultItemSlotTvp> vaultItems,
        CancellationToken ct)
    {
        var builder = new StoredProcedureParametersBuilder("game", "usp_AccountVault_TransferItemWithCharacter", 0)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("Container", container, SqlDbType.TinyInt);

        if (items.Count > 0)
            builder.AddTvpParameter("Items", items);

        builder.AddParameter("AccountId", accountId, SqlDbType.Int);

        if (vaultItems.Count > 0)
            builder.AddTvpParameter("VaultItems", vaultItems);

        await Db.ExecuteAsync(builder.Build(), ct);
    }

    public async ValueTask SetItemsAsync(int accountId, IReadOnlyList<AccountVaultItemSlotTvp> items,
        CancellationToken ct)
    {
        var builder = new StoredProcedureParametersBuilder("game", "usp_AccountVault_SetItems", 0)
            .AddParameter("AccountId", accountId, SqlDbType.Int);

        if (items.Count > 0)
            builder.AddTvpParameter("Items", items);

        await Db.ExecuteAsync(builder.Build(), ct);
    }
}
