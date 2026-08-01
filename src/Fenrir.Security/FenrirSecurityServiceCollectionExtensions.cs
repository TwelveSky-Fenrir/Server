using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Security;

public static class FenrirSecurityServiceCollectionExtensions
{
    public static IServiceCollection AddFenrirSecurity(this IServiceCollection services)
    {
        services.AddSingleton<ApplicationFirewall>();
        return services;
    }
}
