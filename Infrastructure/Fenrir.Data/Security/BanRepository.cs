using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Security;

namespace Fenrir.Data.Security;

public sealed record BanRepository(ICaeriusNetDbContext Db) : IBanRepository
{
    public async ValueTask<bool> IsActiveForAccountAsync(int accountId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_Ban_GetActiveForAccount", 1)
            .AddParameter("AccountId", accountId, SqlDbType.Int)
            .AddInMemoryCache($"admin:ban-active-account:{accountId}", TimeSpan.FromSeconds(2))
            .Build();

        var rows = await Db.QueryAsReadOnlyCollectionAsync<BanRowDto>(sp, ct);
        return rows.Count > 0;
    }

    public async ValueTask<bool> IsActiveForCharacterAsync(int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_Ban_GetActiveForCharacter", 1)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .AddInMemoryCache($"admin:ban-active-character:{characterId}", TimeSpan.FromSeconds(2))
            .Build();

        var rows = await Db.QueryAsReadOnlyCollectionAsync<BanRowDto>(sp, ct);
        return rows.Count > 0;
    }

    public async ValueTask<int> CreateAsync(int? accountId, int? characterId, BanReason reason,
        DateTime? expiresAtUtc, CancellationToken ct, int? actorAccountId = null, int? actorCharacterId = null)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_Ban_Create", 1)
            .AddParameter("AccountId", (object?)accountId ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("CharacterId", (object?)characterId ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("Reason", (byte)reason, SqlDbType.TinyInt)
            .AddParameter("ExpiresAtUtc", (object?)expiresAtUtc ?? DBNull.Value, SqlDbType.DateTime2)
            .AddParameter("ActorAccountId", (object?)actorAccountId ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("ActorCharacterId", (object?)actorCharacterId ?? DBNull.Value, SqlDbType.Int)
            .Build();

        return await Db.ExecuteScalarAsync<int>(sp, ct);
    }
}
