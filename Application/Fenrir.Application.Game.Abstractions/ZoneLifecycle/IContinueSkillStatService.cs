using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

/// <summary>Business logic for CZ_CONTINUE_SKILL_STAT_SEND (op94) -- see <c>ContinueSkillStatHandler</c>'s remarks.</summary>
public interface IContinueSkillStatService
{
    public void RegisterAutoBuffs(Zone zone, int characterId, PlayerRuntimeState state, int[] skill);
}
