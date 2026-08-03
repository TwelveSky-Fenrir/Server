using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Accounts;
using Fenrir.Data.Abstractions.Admin;
using Fenrir.Data.Abstractions.Characters;
using Fenrir.Data.Abstractions.Commerce;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Data.Abstractions.Guilds;
using Fenrir.Data.Abstractions.Inventory;
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
using Fenrir.Data.Inventory;
using Fenrir.Data.Progression;
using Fenrir.Data.Runtime;
using Fenrir.Data.Security;
using Fenrir.Data.Social;
using Fenrir.Data.Tribes;
using Fenrir.Data.World;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BigMoneyRepository = Fenrir.Data.Inventory.BigMoneyRepository;
using IBigMoneyRepository = Fenrir.Data.Abstractions.Inventory.IBigMoneyRepository;
using CharacterBigMoneyRepository = Fenrir.Data.Characters.BigMoneyRepository;
using ICharacterBigMoneyRepository = Fenrir.Data.Abstractions.Characters.IBigMoneyRepository;

namespace Fenrir.Data;

public static class FenrirDataServiceCollectionExtensions
{
    public static IHostApplicationBuilder AddFenrirData(this IHostApplicationBuilder builder,
        string connectionName = "FenrirDb")
    {
        CaeriusNetBuilder
            .Create(builder)
            .WithAspireSqlServer(connectionName)
            .Build();

        var dbContextDescriptor = builder.Services.Single(d => d.ServiceType == typeof(ICaeriusNetDbContext));
        builder.Services.Remove(dbContextDescriptor);
        builder.Services.Add(new ServiceDescriptor(typeof(ICaeriusNetDbContext),
            dbContextDescriptor.ImplementationFactory!,
            ServiceLifetime.Singleton));

        builder.Services.AddSingleton<IAccountRepository, AccountRepository>();
        builder.Services.AddSingleton<IAccountPinRepository, AccountPinRepository>();
        builder.Services.AddSingleton<ICharacterRepository, CharacterRepository>();
        builder.Services.AddSingleton<ICharacterRenameRepository, CharacterRenameRepository>();

        builder.Services.AddSingleton<IPetBagRepository, PetBagRepository>();

        builder.Services
            .AddSingleton<IBigMoneyRepository, BigMoneyRepository>();
        builder.Services.AddSingleton<ICharacterBigMoneyRepository, CharacterBigMoneyRepository>();

        builder.Services.AddSingleton<IRuneRepository, RuneRepository>();

        builder.Services.AddSingleton<ITradeCommitRepository, TradeCommitRepository>();

        builder.Services.AddSingleton<IWarPointRepository, WarPointRepository>();

        builder.Services.AddSingleton<IStarterKitRepository, StarterKitRepository>();
        builder.Services.AddSingleton<ISessionTicketRepository, SessionTicketRepository>();
        builder.Services.AddSingleton<IGameServerDirectoryRepository, GameServerDirectoryRepository>();
        builder.Services.AddSingleton<ICharacterShardLocationRepository, CharacterShardLocationRepository>();

        builder.Services.AddSingleton<ICharacterLogoutStateRepository, CharacterLogoutStateRepository>();
        builder.Services.AddSingleton<IAccountSessionRepository, AccountSessionRepository>();
        builder.Services.AddSingleton<IDailyRewardResetRepository, DailyRewardResetRepository>();

        builder.Services.AddSingleton<IGuildTribeBroadcastRelayRepository, GuildTribeBroadcastRelayRepository>();

        builder.Services.AddSingleton<ISocialCrossShardRelayRepository, SocialCrossShardRelayRepository>();

        builder.Services.AddSingleton<IChatCrossShardRelayRepository, ChatCrossShardRelayRepository>();

        builder.Services.AddSingleton<IProxyShopExpirationRelayRepository, ProxyShopExpirationRelayRepository>();

        builder.Services.AddSingleton<IGuildBuffExpiryRelayRepository, GuildBuffExpiryRelayRepository>();

        builder.Services.AddSingleton<IGuildStateRelayRepository, GuildStateRelayRepository>();

        builder.Services.AddSingleton<IRvrSiegeEventRelayRepository, RvrSiegeEventRelayRepository>();

        builder.Services.AddSingleton<IPartyResyncRelayRepository, PartyResyncRelayRepository>();
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
        builder.Services.AddSingleton<IGuildRepository, GuildRepository>();
        builder.Services.AddSingleton<ITribeRepository, TribeRepository>();
        builder.Services.AddSingleton<ITribeRosterRepository, TribeRosterRepository>();
        builder.Services.AddSingleton<ITribeBankSweepRepository, TribeBankSweepRepository>();
        builder.Services.AddSingleton<ITribeConversionRepository, TribeConversionRepository>();
        builder.Services.AddSingleton<IFourGuildScoringRepository, FourGuildScoringRepository>();
        builder.Services.AddSingleton<IFriendRepository, FriendRepository>();
        builder.Services.AddSingleton<IMentorRepository, MentorRepository>();

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
