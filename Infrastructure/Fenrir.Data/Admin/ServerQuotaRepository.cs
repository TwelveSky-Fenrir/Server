using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using Fenrir.Data.Abstractions.Admin;

namespace Fenrir.Data.Admin;

public sealed record ServerQuotaRepository(ICaeriusNetDbContext Db) : IServerQuotaRepository
{
    public async ValueTask<ServerQuotaDto> GetAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_ServerQuota_GetMaxPlayers", 1).Build();

        return await Db.FirstQueryAsync<ServerQuotaDto>(sp, ct)
               ?? throw new InvalidOperationException(
                   "admin.ServerQuota has no row -- Migrations/Seed/admin/008_server_quota.sql did not run.");
    }
}
