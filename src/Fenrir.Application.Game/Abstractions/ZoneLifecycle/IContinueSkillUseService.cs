using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

public interface IContinueSkillUseService
{
    public ValueTask<AutoBuffActivationResolver.Result> ActivateAsync(Zone zone, int characterId,
        PlayerRuntimeState state, int sort, CancellationToken cancellationToken);
}
