using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Game;

namespace Fenrir.Data.Game;

public sealed record EventLogRepository(ICaeriusNetDbContext Db) : IEventLogRepository
{
    public async ValueTask LogAsync(
        short eventCode,
        EventLogCategory category,
        int? actorAccountId,
        int? actorCharacterId,
        int? targetAccountId,
        int? targetCharacterId,
        short? shardId,
        long? deltaMoney,
        long? deltaBigMoney,
        int? itemId,
        int? quantity,
        byte? outcome,
        string? payload,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("game", "usp_EventLog_Insert", 0)
            .AddParameter("EventCode", eventCode, SqlDbType.SmallInt)
            .AddParameter("Category", (byte)category, SqlDbType.TinyInt)
            .AddParameter("ActorAccountId", (object?)actorAccountId ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("ActorCharacterId", (object?)actorCharacterId ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("TargetAccountId", (object?)targetAccountId ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("TargetCharacterId", (object?)targetCharacterId ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("ShardId", (object?)shardId ?? DBNull.Value, SqlDbType.SmallInt)
            .AddParameter("DeltaMoney", (object?)deltaMoney ?? DBNull.Value, SqlDbType.BigInt)
            .AddParameter("DeltaBigMoney", (object?)deltaBigMoney ?? DBNull.Value, SqlDbType.BigInt)
            .AddParameter("ItemId", (object?)itemId ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("Quantity", (object?)quantity ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("Outcome", (object?)outcome ?? DBNull.Value, SqlDbType.TinyInt)
            .AddParameter("Payload", (object?)payload ?? DBNull.Value, SqlDbType.NVarChar)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask BatchLogAsync(IReadOnlyList<EventLogEntryTvp> rows, CancellationToken ct)
    {
        if (rows.Count == 0)
            return; // SQL Server rejects an empty TVP outright -- never build the call for nothing to flush

        var sp = new StoredProcedureParametersBuilder("game", "usp_EventLog_InsertBatch", 0)
            .AddTvpParameter("Entries", rows)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }
}
