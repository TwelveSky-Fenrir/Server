using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Progression;

public readonly record struct AutoPotionThresholdResult(bool Aborted);

public interface IAutoPotionThresholdService
{
    public ValueTask<AutoPotionThresholdResult> ApplyAsync(int characterId, PlayerRuntimeState state, int value01,
        int value02, CancellationToken cancellationToken);
}
