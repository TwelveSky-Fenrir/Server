using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Application.Game.Handlers.ItemModification.Services;

/// <summary>
///     Registers the business-logic services extracted from the item-modification handlers (combine, craft,
///     destroy, enchant, reroll, rune socket, rank/cape/sky upgrades). All singletons -- same "stateless
///     orchestrator" lifetime reasoning as the repositories they wrap.
/// </summary>
public static class ItemModificationServicesServiceCollectionExtensions
{
    public static IServiceCollection AddItemModificationServices(this IServiceCollection services)
    {
        services.AddSingleton<ICombineItemService, CombineItemService>();
        services.AddSingleton<ICraftItemService, CraftItemService>();
        services.AddSingleton<ICraftLegendaryPetService, CraftLegendaryPetService>();
        services.AddSingleton<ICraftPetService, CraftPetService>();
        services.AddSingleton<ICraftSkillBookService, CraftSkillBookService>();
        services.AddSingleton<IDestroyItemService, DestroyItemService>();
        services.AddSingleton<IDowngradeItemRankService, DowngradeItemRankService>();
        services.AddSingleton<IEnchantItemService, EnchantItemService>();
        services.AddSingleton<IRerollItemService, RerollItemService>();
        services.AddSingleton<IRuneSocketService, RuneSocketService>();
        services.AddSingleton<ISkyUpgradeItemService, SkyUpgradeItemService>();
        services.AddSingleton<IUpgradeCapeService, UpgradeCapeService>();
        services.AddSingleton<IUpgradeItemRankService, UpgradeItemRankService>();

        return services;
    }
}
