using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Data.Runtime;

// Cross-shard character-location directory (runtime.CharacterShardLocation) -- see
// ICharacterShardLocationRepository for the per-method contract.
public sealed record CharacterShardLocationRepository(ICaeriusNetDbContext Db) : ICharacterShardLocationRepository
{
    // In-memory OLTP table, sub-millisecond procs -- a short timeout fails fast instead of masking a stuck request.
    private const int CommandTimeoutSeconds = 5;

    public ValueTask UpsertAsync(int characterId, byte shardId, short mapId, string avatarName, byte tribe,
        CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("runtime", "usp_CharacterShardLocation_Upsert", 0,
                CommandTimeoutSeconds)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("ShardId", shardId, SqlDbType.TinyInt)
            .AddParameter("MapId", mapId, SqlDbType.SmallInt)
            .AddParameter("AvatarName", avatarName, SqlDbType.NVarChar)
            .AddParameter("Tribe", tribe, SqlDbType.TinyInt)
            .Build();

        return Db.ExecuteAsync(sp, ct);
    }

    public ValueTask RemoveAsync(int characterId, byte shardId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("runtime", "usp_CharacterShardLocation_Remove", 0,
                CommandTimeoutSeconds)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddParameter("ShardId", shardId, SqlDbType.TinyInt)
            .Build();

        return Db.ExecuteAsync(sp, ct);
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
