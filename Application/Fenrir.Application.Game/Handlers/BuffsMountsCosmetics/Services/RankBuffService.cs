using Fenrir.Application.Game.Buffs;
using Fenrir.Application.Game.World;

namespace Fenrir.Application.Game.Handlers.BuffsMountsCosmetics.Services;

/// <summary>Business logic behind <see cref="RankBuffHandler" /> (CZ_RANK_BUFF_SEND, op111).</summary>
public interface IRankBuffService
{
    RankBuffResult Apply(Zone zone, PlayerRuntimeState state, int characterId, int sort);
}

public sealed class RankBuffService : IRankBuffService
{
    /// <summary>
    ///     ReturnSymbolNumNoMon under a no-alliance, no-capture-event default world state -- see RankBuffResolver's
    ///     remarks.
    /// </summary>
    private const int DefaultStoneCount = 1;

    public RankBuffResult Apply(Zone zone, PlayerRuntimeState state, int characterId, int sort)
    {
        var resolved = RankBuffResolver.Resolve(sort, DefaultStoneCount);
        if (!resolved.Succeeded)
            return new RankBuffResult(false);

        var maxLife = state.Stats?.MaxLife ?? state.MaxLife;
        var maxMana = state.Stats?.MaxMana ?? state.MaxMana;

        zone.PostAvatarBuffCommand(new AvatarBuffZoneCommand(characterId, RankBuffType: sort, Life: maxLife,
            Mana: maxMana));

        return new RankBuffResult(true);
    }
}

public readonly record struct RankBuffResult(bool Succeeded);
