using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Domain.Buffs;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Services.BuffsMountsCosmetics;

public sealed class RankBuffService(WorldStateService worldState) : IRankBuffService
{
    public RankBuffResult Apply(Zone zone, PlayerRuntimeState state, int characterId, int sort)
    {
        var resolved = RankBuffResolver.Resolve(sort, CountOwnedTribeSymbols(state.Tribe), state.IsMovingZone,
            worldState.World.TribeSymbolBattle);

        if (!resolved.Succeeded)
            return new RankBuffResult(resolved.Outcome);

        zone.PostAvatarBuffCommand(new AvatarBuffZoneCommand(characterId, RankBuffType: sort, HealToMax: true));

        return new RankBuffResult(RankBuffResolver.Outcome.Success);
    }

    // ReturnSymbolNumNoMon (Server/Header/function.h:3155): the 4 tribe symbol slots only -- the neutral
    // monster symbol is deliberately NOT counted here, unlike ReturnSymbolNum used by the combat modifiers.
    private int CountOwnedTribeSymbols(byte tribe)
    {
        if (tribe >= WorldStateService.TribeCount)
            return 0;

        var ally = worldState.GetAllyOf(tribe);
        var count = 0;

        for (byte slot = 0; slot < WorldStateService.TribeCount; slot++)
        {
            var owner = worldState.GetTribeSymbolOwner(slot);
            if (owner == tribe || (ally is { } allyId && owner == allyId))
                count++;
        }

        return count;
    }
}
