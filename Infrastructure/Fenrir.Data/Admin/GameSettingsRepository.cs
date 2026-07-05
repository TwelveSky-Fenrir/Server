using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;

namespace Fenrir.Data.Admin;

// The one settings row an admin can retune without a redeploy; cached 5 min (not boot-time Frozen) so edits propagate without a restart.
public sealed record GameSettingsRepository(ICaeriusNetDbContext Db) : IGameSettingsRepository
{
    public async ValueTask<GameSettingsDto> GetAsync(CancellationToken ct)
    {
        var sp = new StoredProcedureParametersBuilder("admin", "usp_GameSettings_Get", 1)
            .AddInMemoryCache("admin:game-settings", TimeSpan.FromMinutes(5))
            .Build();

        return await Db.FirstQueryAsync<GameSettingsDto>(sp, ct)
               ?? throw new InvalidOperationException(
                   "admin.GameSettings has no row -- 70_seed/admin/006_game_settings.sql did not run.");
    }
}
