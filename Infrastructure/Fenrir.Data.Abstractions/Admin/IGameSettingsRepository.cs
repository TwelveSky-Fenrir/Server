namespace Fenrir.Data.Abstractions.Admin;

public interface IGameSettingsRepository
{

        public ValueTask<GameSettingsDto> GetAsync(CancellationToken ct);
}
