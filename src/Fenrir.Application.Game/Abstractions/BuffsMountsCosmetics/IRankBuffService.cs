using Fenrir.Application.Game.Domain.Buffs;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;

public interface IRankBuffService
{
    public RankBuffResult Apply(Zone zone, PlayerRuntimeState state, int characterId, int sort);
}

public readonly record struct RankBuffResult(RankBuffResolver.Outcome Outcome)
{
    public bool Succeeded => Outcome == RankBuffResolver.Outcome.Success;
    public bool SilentlyIgnored => Outcome == RankBuffResolver.Outcome.WorldBattleActive;
}
