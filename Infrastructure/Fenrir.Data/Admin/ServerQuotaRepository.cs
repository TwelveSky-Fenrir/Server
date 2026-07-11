using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Admin;

namespace Fenrir.Data.Admin;

public sealed record ServerQuotaRepository(ICaeriusNetDbContext Db) : IServerQuotaRepository
{
    public async ValueTask<int> GetMaxPlayersAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_ServerQuota_GetMaxPlayers", 1).Build();

        return await Db.ExecuteScalarAsync<int>(sp, ct);
    }
}
