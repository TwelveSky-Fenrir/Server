using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using CaeriusNet.Exceptions;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Data.SqlClient;

namespace Fenrir.Data.Runtime;

public sealed record CharacterShardLocationRepository(ICaeriusNetDbContext Db) : ICharacterShardLocationRepository
{
    private const int CommandTimeoutSeconds = 5;

    private const int ErrorWriteConflict = 41302;
    private const int ErrorDependencyFailure = 41305;
    private const int ErrorCommitDependencyAborted = 41325;

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
            catch (CaeriusNetSqlException ex)
                when (attempt < MaxWriteConflictAttempts && ex.InnerException is SqlException { Number: var sqlErrorNumber } &&
                      IsWriteConflict(sqlErrorNumber))
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
            catch (CaeriusNetSqlException ex)
                when (attempt < MaxWriteConflictAttempts && ex.InnerException is SqlException { Number: var sqlErrorNumber } &&
                      IsWriteConflict(sqlErrorNumber))
            {
            }
        }
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

    private static bool IsWriteConflict(int errorNumber)
    {
        return errorNumber is ErrorWriteConflict or ErrorDependencyFailure or ErrorCommitDependencyAborted;
    }
}
