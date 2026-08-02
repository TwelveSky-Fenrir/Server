using System.Collections.Immutable;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using CaeriusNet.Exceptions;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Data.SqlClient;

namespace Fenrir.Data.Runtime;

public sealed record ProxyShopExpirationRelayRepository(ICaeriusNetDbContext Db)
    : IProxyShopExpirationRelayRepository
{
    private const int CommandTimeoutSeconds = 5;

    private const int ErrorWriteConflict = 41302;
    private const int ErrorDependencyFailure = 41305;
    private const int ErrorCommitDependencyAborted = 41325;

    private const int MaxWriteConflictAttempts = 3;

    public async ValueTask PublishAsync(ProxyShopExpirationRelayEntry entry, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("runtime", "usp_ProxyShopExpirationRelay_Publish", 0,
                CommandTimeoutSeconds)
            .AddParameter("SourceShardId", entry.SourceShardId, SqlDbType.TinyInt)
            .AddParameter("CharacterId", entry.CharacterId, SqlDbType.Int)
            .AddParameter("NewExpirationDate", entry.NewExpirationDate, SqlDbType.Int)
            .AddParameter("CorrelationId", entry.CorrelationId, SqlDbType.UniqueIdentifier)
            .Build();

        await Db.ExecuteAsync(sp, ct);
    }

    public async ValueTask<ImmutableArray<ProxyShopExpirationRelayDto>> PollAsync(byte shardId,
        int retentionSeconds, CancellationToken ct)
    {
        for (var attempt = 1;; attempt++)
        {
            var sp = new StoredProcedureParametersBuilder("runtime", "usp_ProxyShopExpirationRelay_Poll", 16,
                    CommandTimeoutSeconds)
                .AddParameter("ShardId", shardId, SqlDbType.TinyInt)
                .AddParameter("RetentionSeconds", retentionSeconds, SqlDbType.Int)
                .Build();

            try
            {
                return await Db.QueryAsImmutableArrayAsync<ProxyShopExpirationRelayDto>(sp, ct);
            }
            catch (CaeriusNetSqlException ex)
                when (attempt < MaxWriteConflictAttempts && ex.InnerException is SqlException { Number: var sqlErrorNumber } &&
                      IsWriteConflict(sqlErrorNumber))
            {
            }
        }
    }

    private static bool IsWriteConflict(int errorNumber)
    {
        return errorNumber is ErrorWriteConflict or ErrorDependencyFailure or ErrorCommitDependencyAborted;
    }
}
