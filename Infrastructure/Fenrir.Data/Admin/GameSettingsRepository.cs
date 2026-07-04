using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using CaeriusNet.Commands.Reads;

namespace Fenrir.Data.Admin;

/// <summary>
///     admin.GameSettings access -- the one settings row an admin can retune without a redeploy (see that
///     table's own header for why almost every other numeric constant in this codebase stays a C# const
///     instead: verified byte-exact ports of a legacy value must not silently drift from their source).
///     In-memory cached for 5 minutes (CaeriusNet's AddInMemoryCache) rather than boot-time Frozen: unlike
///     world.* reference data, an admin edit here should propagate without a server restart.
/// </summary>
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
