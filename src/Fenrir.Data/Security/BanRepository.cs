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

    public async ValueTask<int> CreateAsync(BanCreationRequest request, CancellationToken ct)
    {
        request.EnsureValid();

        var sp = new StoredProcedureParametersBuilder("admin", "usp_Ban_Create", 1)
            .AddParameter("AccountId", (object?)request.TargetAccountId ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("CharacterId", (object?)request.TargetCharacterId ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("Reason", (byte)request.Reason, SqlDbType.TinyInt)
            .AddParameter("ExpiresAtUtc", (object?)request.ExpiresAtUtc ?? DBNull.Value, SqlDbType.DateTime2)
            .AddParameter("ActorAccountId", request.ActorAccountId, SqlDbType.Int)
            .AddParameter("ActorCharacterId", request.ActorCharacterId, SqlDbType.Int)
            .AddParameter("CorrelationId", request.CorrelationId, SqlDbType.UniqueIdentifier)
            .AddParameter("AuditPayload", request.AuditPayload, SqlDbType.NVarChar)
            .Build();

        return await Db.ExecuteScalarAsync<int>(sp, ct);
    }
}
