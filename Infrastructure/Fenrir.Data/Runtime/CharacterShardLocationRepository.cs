using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Data.SqlClient;

namespace Fenrir.Data.Runtime;

public sealed record CharacterShardLocationRepository(ICaeriusNetDbContext Db) : ICharacterShardLocationRepository
{
    private const int CommandTimeoutSeconds = 5;

    private const int ErrorWriteConflict = 41302;
    private const int ErrorDependencyFailure = 41305;
    private const int ErrorCommitDependencyAborted = 41325;

    // runtime.CharacterShardLocation's PK is HASH(CharacterId) only (no ShardId component) -- a fast
    // reconnect to a different shard racing a delayed disconnect cleanup on the OLD shard provably targets the
    // SAME physical row from two different processes (see usp_CharacterShardLocation_Remove.sql's own header
    // comment for this exact scenario). Both procs are natively compiled against a memory-optimized table, so
    // UPDLOCK/ROWLOCK are not an option (rejected outright against MEMORY_OPTIMIZED tables) -- the correct fix
    // is the same bounded, no-backoff retry-on-conflict shape already used by
    // AccountSessionRepository.ClaimOrSignalKickAsync/SessionTicketRepository.ConsumeAsync.
    private const int MaxWriteConflictAttempts = 3;

    public async ValueTask UpsertAsync(int characterId, byte shardId, short mapId, string avatarName, byte tribe,
        CancellationToken ct)
    {
        for (var attempt = 1;; attempt++)
        {
            var sp = new StoredProcedureParametersBuilder("runtime", "usp_CharacterShardLocation_Upsert", 0,
                    CommandTimeoutSeconds)
                .AddParameter("CharacterId", characterId, SqlDbType.Int)
                .AddParameter("ShardId", shardId, SqlDbType.TinyInt)
                .AddParameter("MapId", mapId, SqlDbType.SmallInt)
                .AddParameter("AvatarName", avatarName, SqlDbType.NVarChar)
                .AddParameter("Tribe", tribe, SqlDbType.TinyInt)
                .Build();

            try
            {
                await Db.ExecuteAsync(sp, ct);
                return;
            }
            catch (SqlException ex) when (attempt < MaxWriteConflictAttempts && IsWriteConflict(ex.Number))
            {
            }
        }
    }

    public async ValueTask RemoveAsync(int characterId, byte shardId, CancellationToken ct)
    {
        for (var attempt = 1;; attempt++)
        {
            var sp = new StoredProcedureParametersBuilder("runtime", "usp_CharacterShardLocation_Remove", 0,
                    CommandTimeoutSeconds)
                .AddParameter("CharacterId", characterId, SqlDbType.Int)
                .AddParameter("ShardId", shardId, SqlDbType.TinyInt)
                .Build();

            try
            {
                await Db.ExecuteAsync(sp, ct);
                return;
            }
            catch (SqlException ex) when (attempt < MaxWriteConflictAttempts && IsWriteConflict(ex.Number))
            {
            }
        }
    }

    private static bool IsWriteConflict(int errorNumber)
    {
        return errorNumber is ErrorWriteConflict or ErrorDependencyFailure or ErrorCommitDependencyAborted;
    }

    public ValueTask<CharacterShardLocationDto?> FindByNameAsync(string avatarName, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("runtime", "usp_CharacterShardLocation_FindByName", 1,
                CommandTimeoutSeconds)
            .AddParameter("AvatarName", avatarName, SqlDbType.NVarChar)
            .Build();

        return Db.FirstQueryAsync<CharacterShardLocationDto>(sp, ct);
    }

    public ValueTask<CharacterShardLocationDto?> FindByCharacterIdAsync(int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("runtime", "usp_CharacterShardLocation_FindByCharacterId", 1,
                CommandTimeoutSeconds)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        return Db.FirstQueryAsync<CharacterShardLocationDto>(sp, ct);
    }
}
