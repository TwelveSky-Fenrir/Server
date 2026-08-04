using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

public interface IContinueSkillStatService
{
    public ValueTask<bool> RegisterAutoBuffsAsync(Zone zone, int characterId, PlayerRuntimeState state, int[] skill,
        CancellationToken cancellationToken);
}
