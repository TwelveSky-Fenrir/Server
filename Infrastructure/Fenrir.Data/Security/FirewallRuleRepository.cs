using System.Collections.Immutable;
using System.Data;
using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Security;

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
    ///     See this method's own remarks on <see cref="IFirewallRuleRepository" /> for why this upserts rather
    ///     than inserts. The 2-second <see cref="GetAllAsync" /> cache means a freshly-blocked IP may still read
    ///     as allowed for up to that long afterward -- an accepted trade-off already established by
    ///     <see cref="IsBlockedAsync" />, not something this write needs to invalidate/bypass.
    /// </summary>
    public async ValueTask BlockAsync(string ipAddress, CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_FirewallRule_Upsert", 2)
            .AddParameter("IpAddress", ipAddress, SqlDbType.VarChar)
            .AddParameter("RuleType", RuleTypeAnyBlock, SqlDbType.TinyInt)
            .Build();

        await Db.ExecuteAsync(sp, ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     Workstream D3 -- the DB half of <c>FirewallAllowlistReconcileService</c>'s legacy <c>RemoveIPTick</c>
    ///     reconcile (<c>Server/ts25firewall/main.cpp:682-698</c>). Implements only the ONE of legacy's three
    ///     reconcile sub-steps Fenrir's schema can support without inventing data:
    ///     <list type="bullet">
    ///         <item>
    ///             <b>"Prune stale allow rows" (main.cpp:684) -- implemented, as its own faithful degenerate
    ///             case.</b> Legacy deletes an ALLOW-designated row unless its IP matches a currently-known
    ///             member; Fenrir tracks no per-account "current IP" roster anywhere in its schema at all (a
    ///             schema sweep of <c>auth.Accounts</c> and <c>runtime.AccountSessions</c> found neither has an
    ///             IP column -- confirmed absent, not merely unchecked), so under this repository's model every
    ///             allow-designated row is unconditionally "stale" and is deleted. This has zero effect on who
    ///             is actually blocked: <see cref="IsBlockedAsync" /> (and <c>IpFloodGuard</c>, the only writer
    ///             of any row at all today) never reads or writes an allow-designated row -- see this type's own
    ///             class remarks on why the ALLOW/ALLOW_CF/ALLOW_IPRANGE values are "deliberately inert" at
    ///             Fenrir's application layer. Pruning them is pure table hygiene, not a behavior change.
    ///         </item>
    ///         <item>
    ///             <b>"Reseed 11 fixed infrastructure IPs" (main.cpp:685-695) -- deliberately NOT implemented.</b>
    ///             The verified behavior contract for this reconcile does not quote the 11 literal IP address
    ///             values (5 of the 11 are flagged there as having no recoverable real-world purpose at all from
    ///             the evidence available). Inventing placeholder addresses would violate this project's
    ///             no-guessed-schema/no-invented-legacy-data rule. Preserved as an explicit open item: a future
    ///             contract re-reading <c>Server/ts25firewall/main.cpp:685-695</c> directly would need to supply
    ///             the real literal values before this step can be added.
    ///         </item>
    ///         <item>
    ///             <b>"Resync every current account IP" (main.cpp:698) -- deliberately NOT implemented.</b>
    ///             Requires a per-account "current IP" column Fenrir's schema does not have anywhere (see
    ///             above). Adding one is a schema/product decision (what counts as "current" -- last login?
    ///             last zone entry? -- and who writes it) outside a security-hardening pass's scope, not
    ///             something to invent here; same "not modeled yet, needs a product decision" posture as
    ///             several <c>PlayerRuntimeState</c> fields carry today (e.g. <c>WarPoint</c>,
    ///             <c>PetExpX2Time</c>).
    ///         </item>
    ///     </list>
    ///     Both un-implemented steps are open items for a follow-up contract, not silently done or silently
    ///     skipped -- see <c>IFirewallRuleRepository.ReconcileAllowlistAsync</c>'s own summary.
    /// </summary>
    public async ValueTask ReconcileAllowlistAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_FirewallRule_ReconcileAllowlist").Build();
        await Db.ExecuteAsync(sp, ct).ConfigureAwait(false);
    }

    /// <summary>
    ///     Short in-memory cache (mirrors GameServerDirectoryRepository's "shards:directory" convention): a GM's
    ///     new rule takes effect within a couple seconds, never needing a server restart.
    /// </summary>
    private ValueTask<ImmutableArray<FirewallRuleRowDto>> GetAllAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_FirewallRule_GetAll")
            .AddInMemoryCache("admin:firewall-rules", TimeSpan.FromSeconds(2))
            .Build();

        return Db.QueryAsImmutableArrayAsync<FirewallRuleRowDto>(sp, ct);
    }
}
