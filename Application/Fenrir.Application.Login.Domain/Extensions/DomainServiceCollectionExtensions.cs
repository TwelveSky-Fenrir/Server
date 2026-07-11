using Fenrir.Application.Login.Domain.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Domain.Extensions;

public static class DomainServiceCollectionExtensions
{
    public static IServiceCollection AddLoginDomain(this IServiceCollection services)
    {
        services.AddSingleton<IValidateOptions<LoginServerOptions>, LoginServerOptionsValidator>();
        services.AddOptions<LoginServerOptions>().ValidateOnStart();

        services.AddSingleton<LoginIpRateLimiter>();
        services.AddSingleton<LoginCapacityState>();

        return services;
    }
}
