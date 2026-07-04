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

/// <summary>
///     Aspire-friendly registration entry point for the entire <c>Fenrir.Data</c> surface (architecture
///     reference §11.1): wires CaeriusNet to the connection resource injected by the AppHost, then
///     registers the repositories as singletons -- the CaeriusNet context owns connection pooling, so one
///     instance per repository is correct, not a bottleneck (§11.1).
/// </summary>
/// <remarks>
///     Not yet called from Fenrir.LoginServer or Fenrir.GameServer -- that Program.cs wiring is Phase 5/6/7.
///     This extension is ready to be invoked once they exist.
/// </remarks>
/// <remarks>
///     Deliberately does not register <see cref="WriteBehind.DirtyTracker{TKey}" />/
///     <see cref="WriteBehind.WriteBehindFlusher{TKey}" />: both are open generics that need a concrete
///     <c>TKey</c> and a flush callback wired to real repositories (which character, which shard, which
///     batch proc) -- that binding belongs to the Phase 6 GameServer host, not to this assembly-wide entry
///     point.
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

        builder.Services.AddSingleton<IAccountRepository, AccountRepository>();
        builder.Services.AddSingleton<IAccountPinRepository, AccountPinRepository>();
        builder.Services.AddSingleton<ICharacterRepository, CharacterRepository>();
        builder.Services.AddSingleton<ICharacterRenameRepository, CharacterRenameRepository>();
        builder.Services.AddSingleton<ISessionTicketRepository, SessionTicketRepository>();
        builder.Services.AddSingleton<IGameServerDirectoryRepository, GameServerDirectoryRepository>();
        builder.Services.AddSingleton<IShardMapAssignmentRepository, ShardMapAssignmentRepository>();
        builder.Services.AddSingleton<IGameSettingsRepository, GameSettingsRepository>();

        // Phase C/V6 Social.
        builder.Services.AddSingleton<IMuteRepository, MuteRepository>();
        builder.Services.AddSingleton<IGuildRepository, GuildRepository>();
        builder.Services.AddSingleton<ITribeRepository, TribeRepository>();
        builder.Services.AddSingleton<IFriendRepository, FriendRepository>();
        builder.Services.AddSingleton<IMentorRepository, MentorRepository>();

        // Server Logic V9 Progression.
        builder.Services.AddSingleton<IHeroRankingRepository, HeroRankingRepository>();
        builder.Services.AddSingleton<ITowerRepository, TowerRepository>();

        // Server Logic V8 Player Commerce & Cash.
        builder.Services.AddSingleton<ICashRepository, CashRepository>();
        builder.Services.AddSingleton<IOfflineShopRepository, OfflineShopRepository>();
        builder.Services.AddSingleton<IGiftRepository, GiftRepository>();

        return builder;
    }
}
