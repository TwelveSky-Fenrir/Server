using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Writes;
using CaeriusNet.Mappers;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Commerce;
using Microsoft.Data.SqlClient;

namespace Fenrir.Data.Commerce;

public sealed record CashRepository(ICaeriusNetDbContext Db) : ICashRepository
{
    public async ValueTask<int> GetBalanceAsync(int accountId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Cash_GetBalance", 1)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .Build();

        return await Db.ExecuteScalarAsync<int>(sp, ct);
    }

    public async ValueTask<int> DebitAndGrantItemAsync(int accountId, int amount, byte reason, int productId,
        int characterId, byte container, IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct,
        int auditItemId, int auditQuantity, int auditSerial)
    {
        var sp = CreateDebitAndGrantItemParameters(accountId, amount, reason, productId, characterId, container,
            items, auditItemId, auditQuantity, auditSerial);

        return await Db.ExecuteScalarAsync<int>(sp, ct);
    }

    public async ValueTask CreditAsync(int accountId, int amount, byte reason, int? productId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_Cash_Credit", 0)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .AddParameter("Amount", amount, SqlDbType.Int)
            .AddParameter("Reason", reason, SqlDbType.TinyInt)
            .AddParameter("ProductId", (object?)productId ?? DBNull.Value, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask<int> CreditAndConsumeItemAsync(int accountId, int amount, byte reason, int? productId,
        int characterId, byte container, IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct)
    {
        if (items.Count == 0)
        {
            var emptyItemsParameters = new StoredProcedureParameters("game", "usp_Cash_CreditAndConsumeItem", 1,
            [
                CreateParameter("AccountId", accountId, SqlDbType.Int),
                CreateParameter("Amount", amount, SqlDbType.Int),
                CreateParameter("Reason", reason, SqlDbType.TinyInt),
                CreateParameter("ProductId", (object?)productId ?? DBNull.Value, SqlDbType.Int),
                CreateParameter("CharacterId", characterId, SqlDbType.Int),
                CreateParameter("Container", container, SqlDbType.TinyInt),
                CreateEmptyItemsParameter()
            ], null!, null, null);

            return await Db.ExecuteScalarAsync<int>(emptyItemsParameters, ct);
        }

        var builder = new StoredProcedureParametersBuilder("game", "usp_Cash_CreditAndConsumeItem", 1)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .AddParameter("Amount", amount, SqlDbType.Int)
            .AddParameter("Reason", reason, SqlDbType.TinyInt)
            .AddParameter("ProductId", (object?)productId ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("Container", container, SqlDbType.TinyInt);

        builder.AddTvpParameter("Items", items);

        return await Db.ExecuteScalarAsync<int>(builder.Build(), ct);
    }

    public async ValueTask<int> DebitAndGrantItemIdempotentAsync(Guid operationId, byte[] idempotencyKeyHash,
        byte[] requestHash, int accountId, int amount, byte reason, int productId, int characterId, byte container,
        IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct, int auditItemId, int auditQuantity,
        int auditSerial)
    {
        ArgumentNullException.ThrowIfNull(idempotencyKeyHash);
        ArgumentNullException.ThrowIfNull(requestHash);

        if (operationId == Guid.Empty || idempotencyKeyHash.Length != 32 || requestHash.Length != 32)
            throw new ArgumentException("An idempotent cash purchase requires complete operation identity hashes.");

        var sp = CreateDebitAndGrantItemIdempotentParameters(operationId, idempotencyKeyHash, requestHash,
            accountId, amount, reason, productId, characterId, container, items, auditItemId, auditQuantity,
            auditSerial);

        return await Db.ExecuteScalarAsync<int>(sp, ct);
    }

    internal static StoredProcedureParameters CreateDebitAndGrantItemParameters(int accountId, int amount,
        byte reason, int productId, int characterId, byte container, IReadOnlyList<CharacterItemSlotTvp> items,
        int auditItemId, int auditQuantity, int auditSerial)
    {
        if (items.Count == 0)
            return new StoredProcedureParameters("game", "usp_Cash_DebitAndGrantItem", 1,
            [
                CreateParameter("AccountId", accountId, SqlDbType.Int),
                CreateParameter("Amount", amount, SqlDbType.Int),
                CreateParameter("Reason", reason, SqlDbType.TinyInt),
                CreateParameter("ProductId", productId, SqlDbType.Int),
                CreateParameter("CharacterId", characterId, SqlDbType.Int),
                CreateParameter("Container", container, SqlDbType.TinyInt),
                CreateEmptyItemsParameter(),
                CreateParameter("AuditItemId", auditItemId, SqlDbType.Int),
                CreateParameter("AuditQuantity", auditQuantity, SqlDbType.Int),
                CreateParameter("AuditSerial", auditSerial, SqlDbType.Int)
            ], null!, null, null);

        var builder = new StoredProcedureParametersBuilder("game", "usp_Cash_DebitAndGrantItem", 1)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .AddParameter("Amount", amount, SqlDbType.Int)
            .AddParameter("Reason", reason, SqlDbType.TinyInt)
            .AddParameter("ProductId", productId, SqlDbType.Int)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("Container", container, SqlDbType.TinyInt);

        builder.AddTvpParameter("Items", items);

        builder.AddParameter("AuditItemId", auditItemId, SqlDbType.Int)
            .AddParameter("AuditQuantity", auditQuantity, SqlDbType.Int)
            .AddParameter("AuditSerial", auditSerial, SqlDbType.Int);

        return builder.Build();
    }

    internal static StoredProcedureParameters CreateDebitAndGrantItemIdempotentParameters(Guid operationId,
        byte[] idempotencyKeyHash, byte[] requestHash, int accountId, int amount, byte reason, int productId,
        int characterId, byte container, IReadOnlyList<CharacterItemSlotTvp> items, int auditItemId,
        int auditQuantity, int auditSerial)
    {
        if (items.Count == 0)
            return new StoredProcedureParameters("game", "usp_Cash_DebitAndGrantItem_Idempotent", 1,
            [
                CreateParameter("OperationId", operationId, SqlDbType.UniqueIdentifier),
                CreateParameter("IdempotencyKeyHash", idempotencyKeyHash, SqlDbType.Binary),
                CreateParameter("RequestHash", requestHash, SqlDbType.Binary),
                CreateParameter("AccountId", accountId, SqlDbType.Int),
                CreateParameter("Amount", amount, SqlDbType.Int),
                CreateParameter("Reason", reason, SqlDbType.TinyInt),
                CreateParameter("ProductId", productId, SqlDbType.Int),
                CreateParameter("CharacterId", characterId, SqlDbType.Int),
                CreateParameter("Container", container, SqlDbType.TinyInt),
                CreateEmptyItemsParameter(),
                CreateParameter("AuditItemId", auditItemId, SqlDbType.Int),
                CreateParameter("AuditQuantity", auditQuantity, SqlDbType.Int),
                CreateParameter("AuditSerial", auditSerial, SqlDbType.Int)
            ], null!, null, null);

        var builder = new StoredProcedureParametersBuilder("game", "usp_Cash_DebitAndGrantItem_Idempotent", 1)
            .AddParameter("OperationId", operationId, SqlDbType.UniqueIdentifier)
            .AddParameter("IdempotencyKeyHash", idempotencyKeyHash, SqlDbType.Binary)
            .AddParameter("RequestHash", requestHash, SqlDbType.Binary)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .AddParameter("Amount", amount, SqlDbType.Int)
            .AddParameter("Reason", reason, SqlDbType.TinyInt)
            .AddParameter("ProductId", productId, SqlDbType.Int)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("Container", container, SqlDbType.TinyInt);

        if (items.Count > 0) builder.AddTvpParameter("Items", items);

        builder.AddParameter("AuditItemId", auditItemId, SqlDbType.Int)
            .AddParameter("AuditQuantity", auditQuantity, SqlDbType.Int)
            .AddParameter("AuditSerial", auditSerial, SqlDbType.Int);

        return builder.Build();
    }

    private static SqlParameter CreateParameter(string name, object value, SqlDbType sqlDbType)
    {
        return new SqlParameter(name, sqlDbType) { Value = value };
    }

    private static SqlParameter CreateEmptyItemsParameter()
    {
        var table = new DataTable();
        table.Columns.Add("Slot", typeof(byte));
        table.Columns.Add("ItemId", typeof(int));
        table.Columns.Add("Quantity", typeof(int));
        table.Columns.Add("Enchant", typeof(byte));
        table.Columns.Add("Combine", typeof(byte));
        table.Columns.Add("Refine", typeof(byte));
        table.Columns.Add("Socket", typeof(byte));
        table.Columns.Add("SocketGem1", typeof(int));
        table.Columns.Add("SocketGem2", typeof(int));
        table.Columns.Add("SocketGem3", typeof(int));
        table.Columns.Add("ExpireDate", typeof(int));
        table.Columns.Add("Serial", typeof(int));
        table.Columns.Add("XPos", typeof(byte));
        table.Columns.Add("YPos", typeof(byte));

        return new SqlParameter("Items", SqlDbType.Structured)
        {
            TypeName = "game.tvp_CharacterItemSlot",
            Value = table
        };
    }
}
