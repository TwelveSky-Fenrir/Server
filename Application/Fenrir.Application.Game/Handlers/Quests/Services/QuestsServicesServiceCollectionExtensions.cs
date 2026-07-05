using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Application.Game.Handlers.Quests.Services;

/// <summary>Registers the business-logic services extracted from the Quests packet handlers.</summary>
public static class QuestsServicesServiceCollectionExtensions
{
    public static IServiceCollection AddQuestsServices(this IServiceCollection services)
    {
        services.AddSingleton<IQuestProgressService, QuestProgressService>();

        return services;
    }
}
