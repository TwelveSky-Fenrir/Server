using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Application.Login.Handlers.Services;

/// <summary>Registers the handler-support services extracted from this project's packet handlers.</summary>
public static class LoginServicesServiceCollectionExtensions
{
    public static IServiceCollection AddLoginServices(this IServiceCollection services)
    {
        services.AddSingleton<IChangeMousePinService, ChangeMousePinService>();
        services.AddSingleton<IVerifyMousePinService, VerifyMousePinService>();
        services.AddSingleton<ICreateMousePinService, CreateMousePinService>();
        services.AddSingleton<IClaimGiftService, ClaimGiftService>();
        services.AddSingleton<IGiftListService, GiftListService>();
        services.AddSingleton<IDeleteAvatarService, DeleteAvatarService>();
        services.AddSingleton<IRenameAvatarService, RenameAvatarService>();
        services.AddSingleton<ICreateAvatarService, CreateAvatarService>();
        services.AddSingleton<IZoneTransferService, ZoneTransferService>();
        services.AddSingleton<ILoginService, LoginService>();

        return services;
    }
}
