using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

public interface IContinueSkillUseService
{
    public AutoBuffActivationResolver.Result Activate(Zone zone, int characterId, PlayerRuntimeState state, int sort);
}
