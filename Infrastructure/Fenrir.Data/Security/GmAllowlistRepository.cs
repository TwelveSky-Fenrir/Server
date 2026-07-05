using System.Collections.Immutable;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;

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

    /// <summary>Short in-memory cache, same rationale as FirewallRuleRepository's own cache.</summary>
    private ValueTask<ImmutableArray<GmAllowlistRowDto>> GetAllAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_GmAllowlist_GetAll", 8)
            .AddInMemoryCache("admin:gm-allowlist", TimeSpan.FromSeconds(2))
            .Build();

        return Db.QueryAsImmutableArrayAsync<GmAllowlistRowDto>(sp, ct);
    }
}
