using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Application.Game.Handlers.BuffsMountsCosmetics.Services;

/// <summary>
///     Registers the handler-support services extracted from the "buffs-mounts-cosmetics" handler batch
///     (playtime/rank buffs, mount select/mount/absorb, pet action mirroring, costume/stellar-core wardrobe).
///     Stateless orchestrators over existing resolvers/repositories, same singleton-lifetime reasoning as the
///     repositories they call.
/// </summary>
public static class BuffsMountsCosmeticsServicesServiceCollectionExtensions
{
    public static IServiceCollection AddBuffsMountsCosmeticsServices(this IServiceCollection services)
    {
        services.AddSingleton<IPlaytimeBuffService, PlaytimeBuffService>();
        services.AddSingleton<IRankBuffService, RankBuffService>();
        services.AddSingleton<IMountAbsorbService, MountAbsorbService>();
        services.AddSingleton<IMountStateService, MountStateService>();
        services.AddSingleton<IPetActionUpdateService, PetActionUpdateService>();
        services.AddSingleton<ICostumeStateService, CostumeStateService>();
        services.AddSingleton<ICostumeVisibilityService, CostumeVisibilityService>();
        services.AddSingleton<IStellarCoreStateService, StellarCoreStateService>();

        return services;
    }
}
