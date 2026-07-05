using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Progression;

/// <summary>
///     Outcome of <see cref="IAutoPotionThresholdService.ApplyAsync" />: <see cref="Aborted" /> means the caller must
///     disconnect the session.
/// </summary>
public readonly record struct AutoPotionThresholdResult(bool Aborted);

public interface IAutoPotionThresholdService
{
    public ValueTask<AutoPotionThresholdResult> ApplyAsync(int characterId, PlayerRuntimeState state, int value01,
        int value02, CancellationToken cancellationToken);
}
