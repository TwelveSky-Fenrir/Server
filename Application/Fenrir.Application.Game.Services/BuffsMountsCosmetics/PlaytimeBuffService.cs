using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Domain.Buffs;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Services.BuffsMountsCosmetics;

public sealed class PlaytimeBuffService : IPlaytimeBuffService
{
    public PlaytimeBuffResult Apply(Zone zone, int characterId, int sort)
    {
        var resolved = PlaytimeBuffResolver.Resolve(sort);

        zone.PostAvatarBuffCommand(new AvatarBuffZoneCommand(
            characterId,
            resolved.Applied ? resolved.NewStateTimeEffect : null,
            PlayTime2: PlaytimeBuffResolver.PlayTimeClobberValue));

        return new PlaytimeBuffResult(resolved.Value);
    }
}
