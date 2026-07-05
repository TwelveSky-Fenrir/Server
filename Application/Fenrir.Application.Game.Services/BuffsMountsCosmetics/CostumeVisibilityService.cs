using Fenrir.Application.Game.Abstractions.BuffsMountsCosmetics;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Services.BuffsMountsCosmetics;

/// <inheritdoc cref="ICostumeVisibilityService" />
public sealed class CostumeVisibilityService : ICostumeVisibilityService
{
    public void Apply(Zone zone, int characterId, int sort)
    {
        zone.PostCostumeCommand(new CostumeZoneCommand(characterId, CostumeState: sort,
            FullActionRebroadcast: true));
    }
}
