using Fenrir.Application.Game.World;

namespace Fenrir.Application.Game.Handlers.BuffsMountsCosmetics.Services;

/// <summary>Business logic behind <see cref="CostumeVisibilityHandler" /> (CZ_COSTUME_STATE2_SEND, op139).</summary>
public interface ICostumeVisibilityService
{
    void Apply(Zone zone, int characterId, int sort);
}

public sealed class CostumeVisibilityService : ICostumeVisibilityService
{
    public void Apply(Zone zone, int characterId, int sort)
    {
        zone.PostCostumeCommand(new CostumeZoneCommand(characterId, CostumeState: sort,
            FullActionRebroadcast: true));
    }
}
