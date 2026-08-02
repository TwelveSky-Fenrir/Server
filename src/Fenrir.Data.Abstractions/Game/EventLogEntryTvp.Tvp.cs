using System.Data;
using CaeriusNet.Mappers;
using Microsoft.Data.SqlClient.Server;

namespace Fenrir.Data.Abstractions.Game;

public sealed partial record EventLogEntryTvp : ITvpMapper<EventLogEntryTvp>
{
    private static readonly SqlMetaData[] TvpMetaData =
    [
        new("EventCode", SqlDbType.SmallInt),
        new("Category", SqlDbType.TinyInt),
        new("ActorAccountId", SqlDbType.Int),
        new("ActorCharacterId", SqlDbType.Int),
        new("TargetAccountId", SqlDbType.Int),
        new("TargetCharacterId", SqlDbType.Int),
        new("ShardId", SqlDbType.SmallInt),
        new("DeltaMoney", SqlDbType.BigInt),
        new("DeltaBigMoney", SqlDbType.BigInt),
        new("ItemId", SqlDbType.Int),
        new("Quantity", SqlDbType.Int),
        new("Outcome", SqlDbType.TinyInt),
        new("Payload", SqlDbType.NVarChar, SqlMetaData.Max),
        new("OccurredAtUtc", SqlDbType.DateTime2)
    ];

    public static string TvpTypeName => "game.tvp_EventLogEntry";

    public IEnumerable<SqlDataRecord> MapAsSqlDataRecords(IEnumerable<EventLogEntryTvp> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return Iterator(items);

        static IEnumerable<SqlDataRecord> Iterator(IEnumerable<EventLogEntryTvp> items)
        {
            var record = new SqlDataRecord(TvpMetaData);
            foreach (var item in items)
            {
                record.SetInt16(0, item.EventCode);
                record.SetByte(1, item.Category);
                if (item.ActorAccountId is null) record.SetDBNull(2);
                else record.SetInt32(2, item.ActorAccountId.Value);
                if (item.ActorCharacterId is null) record.SetDBNull(3);
                else record.SetInt32(3, item.ActorCharacterId.Value);
                if (item.TargetAccountId is null) record.SetDBNull(4);
                else record.SetInt32(4, item.TargetAccountId.Value);
                if (item.TargetCharacterId is null) record.SetDBNull(5);
                else record.SetInt32(5, item.TargetCharacterId.Value);
                if (item.ShardId is null) record.SetDBNull(6);
                else record.SetInt16(6, item.ShardId.Value);
                if (item.DeltaMoney is null) record.SetDBNull(7);
                else record.SetInt64(7, item.DeltaMoney.Value);
                if (item.DeltaBigMoney is null) record.SetDBNull(8);
                else record.SetInt64(8, item.DeltaBigMoney.Value);
                if (item.ItemId is null) record.SetDBNull(9);
                else record.SetInt32(9, item.ItemId.Value);
                if (item.Quantity is null) record.SetDBNull(10);
                else record.SetInt32(10, item.Quantity.Value);
                if (item.Outcome is null) record.SetDBNull(11);
                else record.SetByte(11, item.Outcome.Value);
                if (item.Payload is null) record.SetDBNull(12);
                else record.SetString(12, item.Payload);
                record.SetDateTime(13, item.OccurredAtUtc);
                yield return record;
            }
        }
    }
}
