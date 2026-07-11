using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

public interface IContinueSkillStatService
{
    public void RegisterAutoBuffs(Zone zone, int characterId, PlayerRuntimeState state, int[] skill);
}
