using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;

namespace Fenrir.Data.Runtime;

/// <summary>
///     Access to runtime.SessionTickets (architecture reference §12.4). Tickets are single-use and keyed on
///     AccountId, not a generated TicketId (ADR-0005/ADR-0003 A-04): the unmodified legacy client can only prove
///     its own account identity, so a live ticket for that AccountId is the proof of a prior successful login.
/// </summary>
public sealed record SessionTicketRepository(ICaeriusNetDbContext Db)
{
    // In-memory OLTP table, sub-millisecond procs -- a short timeout fails fast instead of masking a stuck request.
    private const int CommandTimeoutSeconds = 5;

    public ValueTask CreateAsync(int accountId, int characterId, byte shardId, int ttlSeconds, CancellationToken ct)
    {
        var parameters =
            new StoredProcedureParametersBuilder("runtime", "usp_SessionTicket_Create", 0, CommandTimeoutSeconds)
                .AddParameter("AccountId", accountId, SqlDbType.Int)
                .AddParameter("CharacterId", characterId, SqlDbType.Int)
                .AddParameter("ShardId", shardId, SqlDbType.TinyInt)
                .AddParameter("TtlSeconds", ttlSeconds, SqlDbType.Int)
                .Build();

        return Db.ExecuteAsync(parameters, ct);
    }

    public ValueTask<ConsumedTicketDto?> ConsumeAsync(int accountId, CancellationToken ct)
    {
        var parameters =
            new StoredProcedureParametersBuilder("runtime", "usp_SessionTicket_Consume", 1, CommandTimeoutSeconds)
                .AddParameter("AccountId", accountId, SqlDbType.Int)
                .Build();

        return Db.FirstQueryAsync<ConsumedTicketDto>(parameters, ct);
    }

    /// <summary>Sweeps every row past ExpiresAtUtc -- no parameters, so no AddParameter call on the builder.</summary>
    public ValueTask PurgeExpiredAsync(CancellationToken ct)
    {
        var parameters =
            new StoredProcedureParametersBuilder("runtime", "usp_SessionTicket_Purge", 0, CommandTimeoutSeconds)
                .Build();

        return Db.ExecuteAsync(parameters, ct);
    }
}
