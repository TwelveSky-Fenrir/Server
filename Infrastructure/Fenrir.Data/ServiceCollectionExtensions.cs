using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Accounts;
using Fenrir.Data.Abstractions.Admin;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Commerce;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Data.Abstractions.Guilds;
using Fenrir.Data.Abstractions.Progression;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Data.Abstractions.Security;
using Fenrir.Data.Abstractions.Social;
using Fenrir.Data.Abstractions.Tribes;
using Fenrir.Data.Abstractions.World;
using Fenrir.Data.Accounts;
using Fenrir.Data.Admin;
using Fenrir.Data.Characters;
using Fenrir.Data.Commerce;
using Fenrir.Data.Game;
using Fenrir.Data.Guilds;
using Fenrir.Data.Progression;
using Fenrir.Data.Runtime;
using Fenrir.Data.Security;
using Fenrir.Data.Social;
using Fenrir.Data.Tribes;
using Fenrir.Data.World;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Fenrir.Data;

// Wires CaeriusNet to the AppHost connection resource, then registers repositories as singletons -- CaeriusNet owns connection pooling, so one instance per repository is correct.
/// <remarks>
///     Does not register DirtyTracker/WriteBehindFlusher: both are open generics needing a concrete TKey and a flush
///     callback wired to real repositories. For the same reason, does not register EventLogQueue either -- its
///     constructor needs a flush callback closing over IEventLogRepository.BatchLogAsync, so it's constructed
///     downstream in Fenrir.Application.Game.Hosting alongside the BackgroundService that owns its RunAsync loop.
/// </remarks>
public static class FenrirDataServiceCollectionExtensions
{
    public static IHostApplicationBuilder AddFenrirData(this IHostApplicationBuilder builder,
        string connectionName = "FenrirDb")
    {
        CaeriusNetBuilder
            .Create(builder)
            .WithAspireSqlServer(connectionName)
            .Build();

        // CaeriusNetBuilder registers ICaeriusNetDbContext as Scoped, which the singleton repositories below
        // can't consume. CaeriusNetDbContext holds no per-scope state (just a logger and a connection factory --
        // DbConnectionAsync opens a brand-new SqlConnection every call), so it's safe to promote to Singleton.
        // Re-register it as Singleton using CaeriusNet's own factory delegate rather than wrapping it in a
        // manually-created scope: calling IServiceProvider.CreateScope() from inside a singleton factory
        // deadlocks the moment that singleton is resolved as part of constructing another singleton (the
        // root ServiceProviderEngineScope's resolution lock isn't reentrant).
        var dbContextDescriptor = builder.Services.Single(d => d.ServiceType == typeof(ICaeriusNetDbContext));
        builder.Services.Remove(dbContextDescriptor);
        builder.Services.Add(new ServiceDescriptor(typeof(ICaeriusNetDbContext),
            dbContextDescriptor.ImplementationFactory!,
            ServiceLifetime.Singleton));

        builder.Services.AddSingleton<IAccountRepository, AccountRepository>();
        builder.Services.AddSingleton<IAccountPinRepository, AccountPinRepository>();
        builder.Services.AddSingleton<ICharacterRepository, CharacterRepository>();
        builder.Services.AddSingleton<ICharacterRenameRepository, CharacterRenameRepository>();
        builder.Services.AddSingleton<IStarterKitRepository, StarterKitRepository>();
        builder.Services.AddSingleton<ISessionTicketRepository, SessionTicketRepository>();
        builder.Services.AddSingleton<IGameServerDirectoryRepository, GameServerDirectoryRepository>();
        builder.Services.AddSingleton<ICharacterShardLocationRepository, CharacterShardLocationRepository>();
        builder.Services.AddSingleton<IAccountSessionRepository, AccountSessionRepository>();
        builder.Services.AddSingleton<IShardMapAssignmentRepository, ShardMapAssignmentRepository>();
        builder.Services.AddSingleton<IGameSettingsRepository, GameSettingsRepository>();
        builder.Services.AddSingleton<IServerQuotaRepository, ServerQuotaRepository>();
        builder.Services.AddSingleton<ITribeFourQuotaRepository, TribeFourQuotaRepository>();

        builder.Services.AddSingleton<IMuteRepository, MuteRepository>();
        builder.Services.AddSingleton<IBanRepository, BanRepository>();
        builder.Services.AddSingleton<IBlockedIpRepository, BlockedIpRepository>();
        builder.Services.AddSingleton<IFirewallRuleRepository, FirewallRuleRepository>();
        builder.Services.AddSingleton<IGmAllowlistRepository, GmAllowlistRepository>();
        builder.Services.AddSingleton<IMacRestrictionRepository, MacRestrictionRepository>();
        builder.Services.AddSingleton<ApplicationFirewall>();
        builder.Services.AddSingleton<IGuildRepository, GuildRepository>();
        builder.Services.AddSingleton<ITribeRepository, TribeRepository>();
        builder.Services.AddSingleton<IFriendRepository, FriendRepository>();
        builder.Services.AddSingleton<IMentorRepository, MentorRepository>();

        // Registered here (not just in Fenrir.Application.Game.Hosting's AddWorldState) so LoginServer's DI
        // container can resolve it too: op18 CL_DELETE_AVATAR_SEND's tribe-role business rule needs a live read
        // of game.TribeVotes (Force Leader election candidacy) with no Application.Game reference from Login --
        // see DeleteAvatarService's own remarks for the full cross-module read-path rationale. WorldStateRepository's
        // only dependency is ICaeriusNetDbContext (already registered above), so this is a plain, side-effect-free
        // repository registration, not a duplicate of Game's in-memory WorldStateService/write-behind cache --
        // Game's own AddWorldState registration is unaffected by this (registering the same concrete type twice
        // just makes the last registration win for GetRequiredService, and both resolve to functionally identical
        // instances since neither carries per-instance state beyond the shared DB context).
        builder.Services.AddSingleton<IWorldStateRepository, WorldStateRepository>();

        builder.Services.AddSingleton<IHeroRankingRepository, HeroRankingRepository>();
        builder.Services.AddSingleton<ITowerRepository, TowerRepository>();

        builder.Services.AddSingleton<ICashRepository, CashRepository>();
        builder.Services.AddSingleton<IOfflineShopRepository, OfflineShopRepository>();
        builder.Services.AddSingleton<IGiftRepository, GiftRepository>();
        builder.Services.AddSingleton<IAccountVaultRepository, AccountVaultRepository>();

        builder.Services.AddSingleton<IEventLogRepository, EventLogRepository>();

        return builder;
    }
}
