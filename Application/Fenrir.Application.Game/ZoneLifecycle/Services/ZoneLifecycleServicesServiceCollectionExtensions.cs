using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Application.Game.ZoneLifecycle.Services;

/// <summary>
///     Registers the stateless orchestrator services extracted out of the zone-lifecycle handlers (world entry,
///     zone handshake/move/ready, heartbeat, avatar action, auto-buff continue, hotkey/inventory item use, and
///     attack) -- same "one singleton instance" lifetime reasoning as the existing repositories.
/// </summary>
public static class ZoneLifecycleServicesServiceCollectionExtensions
{
    public static IServiceCollection AddZoneLifecycleServices(this IServiceCollection services)
    {
        services.AddSingleton<IAvatarActionService, AvatarActionService>();
        services.AddSingleton<IContinueSkillStatService, ContinueSkillStatService>();
        services.AddSingleton<IContinueSkillUseService, ContinueSkillUseService>();
        services.AddSingleton<IEnterWorldService, EnterWorldService>();
        services.AddSingleton<IHeartbeatService, HeartbeatService>();
        services.AddSingleton<IUseHotkeyItemService, UseHotkeyItemService>();
        services.AddSingleton<IUseInventoryItemService, UseInventoryItemService>();
        services.AddSingleton<IZoneHandshakeService, ZoneHandshakeService>();
        services.AddSingleton<IZoneMoveService, ZoneMoveService>();
        services.AddSingleton<IZoneReadyService, ZoneReadyService>();
        services.AddSingleton<IAttackService, AttackService>();

        return services;
    }
}
