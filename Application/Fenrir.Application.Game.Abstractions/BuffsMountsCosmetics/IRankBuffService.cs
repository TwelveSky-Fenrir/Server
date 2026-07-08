using Fenrir.Application.Game.Domain.Buffs;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;

/// <summary>Business logic behind <see cref="RankBuffHandler" /> (CZ_RANK_BUFF_SEND, op111).</summary>
public interface IRankBuffService
{
    public RankBuffResult Apply(Zone zone, PlayerRuntimeState state, int characterId, int sort);
}

/// <summary>
///     Directly wraps <see cref="RankBuffResolver.Outcome" /> (the three-way Rejected/WorldBattleActive/Success
///     split) rather than a bare bool, so <c>RankBuffHandler</c> can distinguish the world-battle silent-no-op
///     from every other (disconnecting) rejection -- see that resolver's own remarks.
/// </summary>
public readonly record struct RankBuffResult(RankBuffResolver.Outcome Outcome)
{
    public bool Succeeded => Outcome == RankBuffResolver.Outcome.Success;
    public bool SilentlyIgnored => Outcome == RankBuffResolver.Outcome.WorldBattleActive;
}
