using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;

/// <summary>Business logic behind <see cref="RankBuffHandler" /> (CZ_RANK_BUFF_SEND, op111).</summary>
public interface IRankBuffService
{
    public RankBuffResult Apply(Zone zone, PlayerRuntimeState state, int characterId, int sort);
}

public readonly record struct RankBuffResult(bool Succeeded);
