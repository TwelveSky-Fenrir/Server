using System.Data;
using System.Net;
using System.Security.Cryptography;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using CaeriusNet.Exceptions;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Data.SqlClient;

namespace Fenrir.Data.Runtime;

public sealed record SessionTicketRepository(ICaeriusNetDbContext Db) : ISessionTicketRepository
{
    private const int CommandTimeoutSeconds = 5;

    private const int ErrorWriteConflict = 41302;
    private const int ErrorDependencyFailure = 41305;
    private const int ErrorCommitDependencyAborted = 41325;

    private const int MaxWriteConflictAttempts = 3;

    public async ValueTask<MintedSessionTicketDto> CreateAsync(int accountId, int characterId, byte shardId,
        int ttlSeconds, Guid sessionToken, short accountGrade, short targetMapId, IPAddress sourceAddress,
        CancellationToken ct)
    {
        var sourceIpPrefix = SessionTicketBinding.PrefixOf(sourceAddress) ??
                             throw new ArgumentException("The handoff source address must be representable.",
                                 nameof(sourceAddress));
        var minted = SessionTicketCapability.Mint();

        try
        {
            for (var attempt = 1;; attempt++)
            {
                var parameters =
                    new StoredProcedureParametersBuilder("runtime", "usp_SessionTicket_Create", 1, CommandTimeoutSeconds)
                        .AddParameter("AccountId", accountId, SqlDbType.Int)
                        .AddParameter("CharacterId", characterId, SqlDbType.Int)
                        .AddParameter("ShardId", shardId, SqlDbType.TinyInt)
                        .AddParameter("TtlSeconds", ttlSeconds, SqlDbType.Int)
                        .AddParameter("SessionToken", sessionToken, SqlDbType.UniqueIdentifier)
                        .AddParameter("AccountGrade", accountGrade, SqlDbType.SmallInt)
                        .AddParameter("TargetMapId", targetMapId, SqlDbType.SmallInt)
                        .AddParameter("CapabilityHash", minted.Hash, SqlDbType.Binary)
                        .AddParameter("SourceIpPrefix", sourceIpPrefix, SqlDbType.VarChar)
                        .Build();

                try
                {
                    var accepted = await Db.ExecuteScalarAsync<bool>(parameters, ct);
                    return accepted
                        ? new MintedSessionTicketDto(minted.Capability)
                        : new MintedSessionTicketDto(string.Empty);
                }
                catch (CaeriusNetSqlException ex)
                    when (attempt < MaxWriteConflictAttempts &&
                          ex.InnerException is SqlException { Number: var sqlErrorNumber } &&
                          IsWriteConflict(sqlErrorNumber))
                {
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(minted.Hash);
        }
    }

    public async ValueTask<ConsumedTicketDto?> ConsumeAsync(string capability, byte expectedShardId,
        short expectedTargetMapId, IPAddress sourceAddress, CancellationToken ct)
    {
        if (!SessionTicketCapability.TryHash(capability, out var capabilityHash))
            return null;

        var sourceIpPrefix = SessionTicketBinding.PrefixOf(sourceAddress) ??
                             throw new ArgumentException("The handoff source address must be representable.",
                                 nameof(sourceAddress));

        try
        {
            for (var attempt = 1;; attempt++)
            {
                var parameters =
                    new StoredProcedureParametersBuilder("runtime", "usp_SessionTicket_Consume", 1, CommandTimeoutSeconds)
                        .AddParameter("CapabilityHash", capabilityHash, SqlDbType.Binary)
                        .AddParameter("ExpectedShardId", expectedShardId, SqlDbType.TinyInt)
                        .AddParameter("ExpectedTargetMapId", expectedTargetMapId, SqlDbType.SmallInt)
                        .AddParameter("SourceIpPrefix", sourceIpPrefix, SqlDbType.VarChar)
                        .Build();

                try
                {
                    return await Db.FirstQueryAsync<ConsumedTicketDto>(parameters, ct);
                }
                catch (CaeriusNetSqlException ex)
                    when (attempt < MaxWriteConflictAttempts &&
                          ex.InnerException is SqlException { Number: var sqlErrorNumber } &&
                          IsWriteConflict(sqlErrorNumber))
                {
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(capabilityHash);
        }
    }

    public async ValueTask RevokeAsync(int accountId, CancellationToken ct)
    {
        for (var attempt = 1;; attempt++)
        {
            var parameters =
                new StoredProcedureParametersBuilder("runtime", "usp_SessionTicket_Revoke", 0, CommandTimeoutSeconds)
                    .AddParameter("AccountId", accountId, SqlDbType.Int)
                    .Build();

            try
            {
                await Db.ExecuteAsync(parameters, ct);
                return;
            }
            catch (CaeriusNetSqlException ex)
                when (attempt < MaxWriteConflictAttempts &&
                      ex.InnerException is SqlException { Number: var sqlErrorNumber } &&
                      IsWriteConflict(sqlErrorNumber))
            {
            }
        }
    }

    public ValueTask PurgeExpiredAsync(CancellationToken ct)
    {
        var parameters =
            new StoredProcedureParametersBuilder("runtime", "usp_SessionTicket_Purge", 0, CommandTimeoutSeconds)
                .Build();

        return Db.ExecuteAsync(parameters, ct);
    }

    private static bool IsWriteConflict(int errorNumber)
    {
        return errorNumber is ErrorWriteConflict or ErrorDependencyFailure or ErrorCommitDependencyAborted;
    }
}
