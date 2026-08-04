using System.Collections.Immutable;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Security;

namespace Fenrir.Data.Security;

public sealed record FirewallRuleRepository(ICaeriusNetDbContext Db) : IFirewallRuleRepository
{
    private const byte RuleTypeTcpBlock = 1;
    private const byte RuleTypeAnyBlock = 3;
    public static readonly TimeSpan AutoBlockDuration = TimeSpan.FromHours(1);

    public async ValueTask<bool> IsBlockedAsync(string ipAddress, CancellationToken ct)
    {
        var rules = await GetAllAsync(ct);

        foreach (var rule in rules)
            if (rule.IpAddress == ipAddress && rule.RuleType is RuleTypeTcpBlock or RuleTypeAnyBlock)
                return true;

        return false;
    }

    public async ValueTask BlockAsync(string ipAddress, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_FirewallRule_Upsert", 0)
            .AddParameter("IpAddress", ipAddress, SqlDbType.VarChar)
            .AddParameter("RuleType", RuleTypeAnyBlock, SqlDbType.TinyInt)
            .AddParameter("TtlSeconds", (int)AutoBlockDuration.TotalSeconds, SqlDbType.Int)
            .Build();

        await Db.ExecuteAsync(sp, ct).ConfigureAwait(false);
    }

    public async ValueTask ReconcileAllowlistAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_FirewallRule_ReconcileAllowlist", 0).Build();
        await Db.ExecuteAsync(sp, ct).ConfigureAwait(false);
    }

    public async ValueTask<int> AddAsync(string ipAddress, byte ruleType, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_FirewallRule_Add", 1)
            .AddParameter("IpAddress", ipAddress, SqlDbType.VarChar)
            .AddParameter("RuleType", ruleType, SqlDbType.TinyInt)
            .Build();

        return await Db.ExecuteScalarAsync<int>(sp, ct);
    }

    public async ValueTask<int> RemoveAsync(string ipAddress, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_FirewallRule_Remove", 1)
            .AddParameter("IpAddress", ipAddress, SqlDbType.VarChar)
            .Build();

        return await Db.ExecuteScalarAsync<int>(sp, ct);
    }

    private ValueTask<ImmutableArray<FirewallRuleRowDto>> GetAllAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_FirewallRule_GetAll")
            .AddInMemoryCache("admin:firewall-rules", TimeSpan.FromSeconds(2))
            .Build();

        return Db.QueryAsImmutableArrayAsync<FirewallRuleRowDto>(sp, ct);
    }
}
