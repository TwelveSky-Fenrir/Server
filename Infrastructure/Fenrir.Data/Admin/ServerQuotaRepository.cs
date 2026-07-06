using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;
using CaeriusNet.Commands.Writes;
using Fenrir.Data.Abstractions.Admin;

namespace Fenrir.Data.Admin;

// The login-time capacity gates' durable store (admin.ServerQuota) -- deliberately NOT cached here (unlike
// GameSettingsRepository's 5-minute AddInMemoryCache): the whole point is that ServerQuotaRefreshHost controls
// the refresh cadence itself (~1s), so every call here must hit the database.
public sealed record ServerQuotaRepository(ICaeriusNetDbContext Db) : IServerQuotaRepository
{
    public async ValueTask<int> GetMaxPlayersAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_ServerQuota_GetMaxPlayers", 1).Build();

        return await Db.ExecuteScalarAsync<int>(sp, ct);
    }
}
