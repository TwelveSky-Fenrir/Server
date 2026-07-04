using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Accounts;
using Fenrir.Data.Admin;
using Fenrir.Data.Characters;
using Fenrir.Data.Commerce;
using Fenrir.Data.Guilds;
using Fenrir.Data.Progression;
using Fenrir.Data.Runtime;
using Fenrir.Data.Social;
using Fenrir.Data.Tribes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Fenrir.Data;

// Wires CaeriusNet to the AppHost connection resource, then registers repositories as singletons -- CaeriusNet owns connection pooling, so one instance per repository is correct.
/// <remarks>
///     Does not register DirtyTracker/WriteBehindFlusher: both are open generics needing a concrete TKey and a flush
///     callback wired to real repositories.
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
        // DbConnectionAsync opens a brand-new SqlConnection every call), so resolving it once from a single
        // root scope and reusing that instance for the app's lifetime is safe. This registration replaces
        // CaeriusNet's Scoped one because the last registration for a service type wins.
        builder.Services.AddSingleton(sp => sp.CreateScope().ServiceProvider.GetRequiredService<ICaeriusNetDbContext>());

        builder.Services.AddSingleton<IAccountRepository, AccountRepository>();
        builder.Services.AddSingleton<IAccountPinRepository, AccountPinRepository>();
        builder.Services.AddSingleton<ICharacterRepository, CharacterRepository>();
        builder.Services.AddSingleton<ICharacterRenameRepository, CharacterRenameRepository>();
        builder.Services.AddSingleton<ISessionTicketRepository, SessionTicketRepository>();
        builder.Services.AddSingleton<IGameServerDirectoryRepository, GameServerDirectoryRepository>();
        builder.Services.AddSingleton<IShardMapAssignmentRepository, ShardMapAssignmentRepository>();
        builder.Services.AddSingleton<IGameSettingsRepository, GameSettingsRepository>();

        builder.Services.AddSingleton<IMuteRepository, MuteRepository>();
        builder.Services.AddSingleton<IGuildRepository, GuildRepository>();
        builder.Services.AddSingleton<ITribeRepository, TribeRepository>();
        builder.Services.AddSingleton<IFriendRepository, FriendRepository>();
        builder.Services.AddSingleton<IMentorRepository, MentorRepository>();

        builder.Services.AddSingleton<IHeroRankingRepository, HeroRankingRepository>();
        builder.Services.AddSingleton<ITowerRepository, TowerRepository>();

        builder.Services.AddSingleton<ICashRepository, CashRepository>();
        builder.Services.AddSingleton<IOfflineShopRepository, OfflineShopRepository>();
        builder.Services.AddSingleton<IGiftRepository, GiftRepository>();

        return builder;
    }
}
