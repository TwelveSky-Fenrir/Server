using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;

public interface ICostumeVisibilityService
{
    public void Apply(Zone zone, int characterId, int sort);
}
