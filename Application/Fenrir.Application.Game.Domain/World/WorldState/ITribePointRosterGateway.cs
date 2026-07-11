using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.WorldState;

public readonly record struct TribeRosterCharacterSnapshot(
    byte TribeId,
    short Level1,
    short Level2,
    short RebirthCount);

public interface ITribePointRosterGateway
{

        public Task<IReadOnlyList<TribeRosterCharacterSnapshot>?> GetRosterAsync(CancellationToken ct);
}

public sealed class LoggingOnlyTribePointRosterGateway(ILogger<LoggingOnlyTribePointRosterGateway> logger)
    : ITribePointRosterGateway
{
    public Task<IReadOnlyList<TribeRosterCharacterSnapshot>?> GetRosterAsync(CancellationToken ct)
    {
        logger.LogWarning(
            "TribePointLevelRecompute due but no character/avatar roster gateway is wired yet -- skipping this run, previous tribe-point totals left unchanged");
        return Task.FromResult<IReadOnlyList<TribeRosterCharacterSnapshot>?>(null);
    }
}
