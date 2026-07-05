using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Domain.Buffs;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Services.BuffsMountsCosmetics;

/// <inheritdoc cref="IRankBuffService" />
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
