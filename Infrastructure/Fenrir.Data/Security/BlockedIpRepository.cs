using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Security;

namespace Fenrir.Data.Security;

public sealed record BlockedIpRepository(ICaeriusNetDbContext Db) : IBlockedIpRepository
{
    // usp_BlockedIp_Exists is WITH NATIVE_COMPILATION against a MEMORY_OPTIMIZED admin.BlockedIps and is
    // checked on every single inbound connection attempt -- same "in-memory OLTP table, sub-millisecond
    // procs" shape as AccountSessionRepository/GameServerDirectoryRepository, which both override the
    // default 30s CommandTimeout down to 5s so a stuck call fails fast instead of masking a problem behind
    // a 30s hang on the hottest security-gating path in the schema.
    private const int CommandTimeoutSeconds = 5;

    // Deliberately NOT cached, unlike the structurally-similar FirewallRuleRepository/GmAllowlistRepository/
    // MacRestrictionRepository/BanRepository (all of which AddInMemoryCache(..., TimeSpan.FromSeconds(2)) over
    // disk-based admin.* security-gate tables): admin.BlockedIps is itself MEMORY_OPTIMIZED and this procedure
    // is natively compiled -- already a sub-millisecond in-memory-OLTP lookup with nothing left for an
    // application-level cache to save. Adding one here would trade an already-negligible query cost for a
    // real security-latency regression: a freshly-blocked IP must stop connecting on its very next attempt,
    // with no tolerance window, unlike the disk-based sibling tables which all accept a ~2s propagation delay.
    // Do not add AddInMemoryCache to this method.
    public async ValueTask<bool> IsBlockedAsync(string ipAddress, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_BlockedIp_Exists", 1, CommandTimeoutSeconds)
            .AddParameter("IpAddress", ipAddress, SqlDbType.VarChar)
            .Build();

        // usp_BlockedIp_Exists is provably 0-or-1 row (UQ_BlockedIps_IpAddress is a UNIQUE hash index on
        // IpAddress) -- FirstQueryAsync is the terminal call that actually matches that cardinality,
        // avoiding QueryAsReadOnlyCollectionAsync's collection allocation on the hottest read path in the
        // whole admin schema (checked on every inbound connection attempt to both servers).
        return await Db.FirstQueryAsync<BlockedIpRowDto>(sp, ct) is not null;
    }

    public async ValueTask<int> AddAsync(string ipAddress, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_BlockedIp_Add", 1)
            .AddParameter("IpAddress", ipAddress, SqlDbType.VarChar)
            .Build();

        return await Db.ExecuteScalarAsync<int>(sp, ct);
    }
}
