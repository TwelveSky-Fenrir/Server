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
            .Build();

        var rows = await Db.QueryAsReadOnlyCollectionAsync<BanRowDto>(sp, ct);
        return rows.Count > 0;
    }

    public async ValueTask<bool> IsActiveForCharacterAsync(int characterId, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_Ban_GetActiveForCharacter", 1)
            .AddParameter("CharacterId", characterId, SqlDbType.Int)
            .Build();

        var rows = await Db.QueryAsReadOnlyCollectionAsync<BanRowDto>(sp, ct);
        return rows.Count > 0;
    }

    public async ValueTask<int> CreateAsync(int? accountId, int? characterId, BanReason reason,
        DateTime? expiresAtUtc, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_Ban_Create", 4)
            .AddParameter("AccountId", (object?)accountId ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("CharacterId", (object?)characterId ?? DBNull.Value, SqlDbType.Int)
            .AddParameter("Reason", (byte)reason, SqlDbType.TinyInt)
            .AddParameter("ExpiresAtUtc", (object?)expiresAtUtc ?? DBNull.Value, SqlDbType.DateTime2)
            .Build();

        return await Db.ExecuteScalarAsync<int>(sp, ct);
    }
}
