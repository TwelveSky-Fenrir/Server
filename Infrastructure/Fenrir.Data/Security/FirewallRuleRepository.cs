using System.Collections.Immutable;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;

namespace Fenrir.Data.Security;

/// <summary>
///     Legacy <c>firewall_ip</c> fed <c>ts25firewall.exe</c>'s Windows-Firewall COM automation (do-not-port,
///     cf. mission cluster C02) -- in that pipeline, only rows typed TCP_BLOCK/ANY_BLOCK ever kept an IP out
///     (<c>Server/ts25firewall/firewall.h:21-26</c>: TCP_ALLOW=0, TCP_BLOCK=1, ANY_ALLOW=2, ANY_BLOCK=3,
///     TCP_ALLOW_CF=4, TCP_ALLOW_IPRANGE=5). The ALLOW/ALLOW_CF/ALLOW_IPRANGE values only ever punched OS
///     firewall holes, which has no equivalent at Fenrir's application layer, so they're deliberately inert here.
///     This repository re-hosts the "keep the IP out" half of that pipeline as an application-level check,
///     since Fenrir never spawns an OS-firewall-automation companion process.
/// </summary>
public sealed record FirewallRuleRepository(ICaeriusNetDbContext Db) : IFirewallRuleRepository
{
    // Server/ts25firewall/firewall.h:21-26.
    private const byte RuleTypeTcpBlock = 1;
    private const byte RuleTypeAnyBlock = 3;

    public async ValueTask<bool> IsBlockedAsync(string ipAddress, CancellationToken ct)
    {
        var rules = await GetAllAsync(ct);

        foreach (var rule in rules)
            if (rule.IpAddress == ipAddress && rule.RuleType is RuleTypeTcpBlock or RuleTypeAnyBlock)
                return true;

        return false;
    }

    /// <summary>
    ///     Short in-memory cache (mirrors GameServerDirectoryRepository's "shards:directory" convention): a GM's
    ///     new rule takes effect within a couple seconds, never needing a server restart.
    /// </summary>
    private ValueTask<ImmutableArray<FirewallRuleRowDto>> GetAllAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_FirewallRule_GetAll", 16)
            .AddInMemoryCache("admin:firewall-rules", TimeSpan.FromSeconds(2))
            .Build();

        return Db.QueryAsImmutableArrayAsync<FirewallRuleRowDto>(sp, ct);
    }
}
