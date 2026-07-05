using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;

/// <summary>Business logic behind <see cref="CostumeVisibilityHandler" /> (CZ_COSTUME_STATE2_SEND, op139).</summary>
public interface ICostumeVisibilityService
{
    public void Apply(Zone zone, int characterId, int sort);
}
