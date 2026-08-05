using System.Collections.Immutable;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Admin;

namespace Fenrir.Data.Admin;

public sealed record MuteRepository(ICaeriusNetDbContext Db) : IMuteRepository
{
    public async ValueTask<bool> IsActiveForCharacterAsync(int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_Mute_GetActiveForCharacter", 1)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddInMemoryCache($"admin:mute-active-character:{characterId}", TimeSpan.FromSeconds(2))
            .Build();

        var rows = await Db.QueryAsReadOnlyCollectionAsync<MuteRowDto>(sp, ct);
        return rows.Count > 0;
    }

    public async ValueTask<ImmutableArray<int>> GetActiveCharacterIdsAsync(IReadOnlyCollection<int> characterIds,
        CancellationToken ct)
    {
        if (characterIds.Count == 0)
            return ImmutableArray<int>.Empty;

        var rows = characterIds.Select(id => new CharacterIdTvp(id)).ToArray();

        var sp = new StoredProcedureParametersBuilder("admin", "usp_Mute_GetActiveForCharacters", characterIds.Count)
            .AddTvpParameter("CharacterIds", rows)
            .Build();

        var muted = await Db.QueryAsImmutableArrayAsync<MutedCharacterIdDto>(sp, ct);
        return muted.IsEmpty ? ImmutableArray<int>.Empty : [..muted.Select(m => m.CharacterId)];
    }

    public async ValueTask<int> CreateAsync(int? accountId, int? characterId, byte reason, DateTime? expiresAtUtc,
        CancellationToken ct, int? actorAccountId = null, int? actorCharacterId = null)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_Mute_Create", 1)
            .AddParameter("AccountId", (object?)accountId ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("CharacterId", (object?)characterId ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("Reason", reason, SqlDbType.TinyInt)
            .AddParameter("ExpiresAtUtc", (object?)expiresAtUtc ?? DBNull.Value, SqlDbType.DateTime2)
            .AddParameter("ActorAccountId", (object?)actorAccountId ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("ActorCharacterId", (object?)actorCharacterId ?? DBNull.Value, SqlDbType.Int)
            .Build();

        return await Db.ExecuteScalarAsync<int>(sp, ct);
    }

    public async ValueTask LiftAsync(int muteId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_Mute_Lift", 0)
            .AddParameter("MuteId", muteId, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct).ConfigureAwait(false);
    }
}
