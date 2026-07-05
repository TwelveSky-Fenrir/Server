namespace Fenrir.Data.Admin;

public interface IGameSettingsRepository
{
    /// <summary>The admin.GameSettings singleton row, in-memory cached (see the repository's own remarks).</summary>
    public ValueTask<GameSettingsDto> GetAsync(CancellationToken ct);
}
