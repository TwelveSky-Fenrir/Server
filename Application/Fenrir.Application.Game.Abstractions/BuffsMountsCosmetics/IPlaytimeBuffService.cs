using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;

/// <summary>Business logic behind <see cref="PlaytimeBuffHandler" /> (CZ_TIME_EFFECT_SEND, op97).</summary>
public interface IPlaytimeBuffService
{
    public PlaytimeBuffResult Apply(Zone zone, int characterId, int sort);
}

public readonly record struct PlaytimeBuffResult(int Value);
