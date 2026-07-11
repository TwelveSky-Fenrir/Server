using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Domain.Buffs;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Services.BuffsMountsCosmetics;

public sealed class RankBuffService(WorldStateService worldState) : IRankBuffService
{
    private const int DefaultStoneCount = 1;

    public RankBuffResult Apply(Zone zone, PlayerRuntimeState state, int characterId, int sort)
    {
        var resolved = RankBuffResolver.Resolve(sort, DefaultStoneCount, state.IsMovingZone,
            worldState.World.TribeSymbolBattle);

        if (!resolved.Succeeded)
            return new RankBuffResult(resolved.Outcome);

        zone.PostAvatarBuffCommand(new AvatarBuffZoneCommand(characterId, RankBuffType: sort, HealToMax: true));

        return new RankBuffResult(RankBuffResolver.Outcome.Success);
    }
}
