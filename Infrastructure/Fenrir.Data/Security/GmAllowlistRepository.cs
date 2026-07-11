using System.Collections.Immutable;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Security;

namespace Fenrir.Data.Security;

public sealed record GmAllowlistRepository(ICaeriusNetDbContext Db) : IGmAllowlistRepository
{
    public async ValueTask<bool> IsAllowedAsync(string ipAddress, CancellationToken ct)
    {
        var rows = await GetAllAsync(ct);

        foreach (var row in rows)
            if (row.IpAddress == ipAddress)
                return true;

        return false;
    }

    public async ValueTask<int> AddAsync(string ipAddress, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_GmAllowlist_Add", 1)
            .AddParameter("IpAddress", ipAddress, SqlDbType.VarChar)
            .Build();

        return await Db.ExecuteScalarAsync<int>(sp, ct);
    }

        private ValueTask<ImmutableArray<GmAllowlistRowDto>> GetAllAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_GmAllowlist_GetAll", 8)
            .AddInMemoryCache("admin:gm-allowlist", TimeSpan.FromSeconds(2))
            .Build();

        return Db.QueryAsImmutableArrayAsync<GmAllowlistRowDto>(sp, ct);
    }
}
