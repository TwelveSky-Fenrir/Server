using Fenrir.Application.Game.Buffs;
using Fenrir.Application.Game.World;

namespace Fenrir.Application.Game.Handlers.BuffsMountsCosmetics.Services;

/// <summary>Business logic behind <see cref="PlaytimeBuffHandler" /> (CZ_TIME_EFFECT_SEND, op97).</summary>
public interface IPlaytimeBuffService
{
    PlaytimeBuffResult Apply(Zone zone, int characterId, int sort);
}

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

public readonly record struct PlaytimeBuffResult(int Value);
