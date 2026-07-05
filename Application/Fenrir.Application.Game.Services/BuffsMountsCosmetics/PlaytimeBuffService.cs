using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Domain.Buffs;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Services.BuffsMountsCosmetics;

/// <inheritdoc cref="IPlaytimeBuffService" />
public sealed class PlaytimeBuffService : IPlaytimeBuffService
{
    public PlaytimeBuffResult Apply(Zone zone, int characterId, int sort)
    {
        var resolved = PlaytimeBuffResolver.Resolve(sort);

        if (resolved.Applied)
            zone.PostAvatarBuffCommand(new AvatarBuffZoneCommand(characterId, resolved.NewStateTimeEffect));

        return new PlaytimeBuffResult(resolved.Value);
    }
}
