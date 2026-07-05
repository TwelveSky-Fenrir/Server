using Fenrir.Application.Game.Domain.Skills;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

/// <summary>Business logic for CZ_CONTINUE_SKILL_USE_SEND (op95) -- see <c>ContinueSkillUseHandler</c>'s remarks.</summary>
public interface IContinueSkillUseService
{
    public AutoBuffActivationResolver.Result Activate(Zone zone, int characterId, PlayerRuntimeState state, int sort);
}
