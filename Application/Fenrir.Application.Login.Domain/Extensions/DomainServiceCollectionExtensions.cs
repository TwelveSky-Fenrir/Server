using Fenrir.Application.Login.Domain.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Domain.Extensions;

/// <summary>
///     Process-wide Login Domain singletons. Does NOT bind <see cref="LoginServerOptions" /> to configuration
///     itself — that call needs the AOT-safe configuration-binding source generator, which is only enabled on
///     FenrirExecutable projects (Directory.Build.targets), not this shared class library. The executable's own
///     Program.cs calls "services.Configure&lt;LoginServerOptions&gt;(configuration.GetSection("Login"))" directly
///     before this method.
/// </summary>
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
