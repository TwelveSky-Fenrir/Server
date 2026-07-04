namespace Fenrir.Data.Admin;

public interface IGameSettingsRepository
{
    /// <summary>The admin.GameSettings singleton row, in-memory cached (see the repository's own remarks).</summary>
    ValueTask<GameSettingsDto> GetAsync(CancellationToken ct);
}
