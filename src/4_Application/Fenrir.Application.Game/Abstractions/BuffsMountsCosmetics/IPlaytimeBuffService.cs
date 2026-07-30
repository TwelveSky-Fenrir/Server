using Fenrir.Application.Game.Domain.World;
namespace Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;

public interface IPlaytimeBuffService
{
    public PlaytimeBuffResult Apply(Zone zone, int characterId, int sort);
}

public readonly record struct PlaytimeBuffResult(int Value);
