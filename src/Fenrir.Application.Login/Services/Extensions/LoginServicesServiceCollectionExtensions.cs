using Fenrir.Application.Login.Abstractions.ChangeMousePin;
using Fenrir.Application.Login.Abstractions.ClaimGift;
using Fenrir.Application.Login.Abstractions.CreateAvatar;
using Fenrir.Application.Login.Abstractions.CreateMousePin;
using Fenrir.Application.Login.Abstractions.DeleteAvatar;
using Fenrir.Application.Login.Abstractions.GiftList;
using Fenrir.Application.Login.Abstractions.Login;
using Fenrir.Application.Login.Abstractions.RenameAvatar;
using Fenrir.Application.Login.Abstractions.RetiredItems;
using Fenrir.Application.Login.Abstractions.VerifyMousePin;
using Fenrir.Application.Login.Abstractions.ZoneTransfer;
using Fenrir.Application.Login.Services.ChangeMousePin;
using Fenrir.Application.Login.Services.ClaimGift;
using Fenrir.Application.Login.Services.CreateAvatar;
using Fenrir.Application.Login.Services.CreateMousePin;
using Fenrir.Application.Login.Services.DeleteAvatar;
using Fenrir.Application.Login.Services.GiftList;
using Fenrir.Application.Login.Services.Login;
using Fenrir.Application.Login.Services.RenameAvatar;
using Fenrir.Application.Login.Services.RetiredItems;
using Fenrir.Application.Login.Services.VerifyMousePin;
using Fenrir.Application.Login.Services.ZoneTransfer;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Application.Login.Services.Extensions;

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
        services.AddSingleton<IRetiredItemPurgeService, RetiredItemPurgeService>();
        services.AddSingleton<IShardReachabilityProbe, TcpShardReachabilityProbe>();
        services.AddSingleton<IZoneTransferService, ZoneTransferService>();
        services.AddSingleton<ILoginService, LoginService>();

        return services;
    }
}
