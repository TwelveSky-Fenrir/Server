using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Application.Game.Handlers.Chat.Services;

/// <summary>
///     Registers every chat-handler business-logic service extracted from Handlers/Chat/*Handler. Singleton,
///     same lifetime reasoning as the existing repositories/registries these services orchestrate over --
///     stateless orchestrators, no per-request state of their own.
/// </summary>
public static class ChatServicesServiceCollectionExtensions
{
    public static IServiceCollection AddChatServices(this IServiceCollection services)
    {
        services.AddSingleton<IGuildAnnouncementService, GuildAnnouncementService>();
        services.AddSingleton<IGuildChatService, GuildChatService>();
        services.AddSingleton<ILocalChatService, LocalChatService>();
        services.AddSingleton<IPartyChatService, PartyChatService>();
        services.AddSingleton<IShoutService, ShoutService>();
        services.AddSingleton<ITribeAnnouncementService, TribeAnnouncementService>();
        services.AddSingleton<ITribeChatService, TribeChatService>();
        services.AddSingleton<IWhisperService, WhisperService>();
        services.AddSingleton<IWorldChatService, WorldChatService>();

        return services;
    }
}
