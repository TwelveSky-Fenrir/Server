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

        // C8-hotkey-money-petbag: game.CharacterPetBag (20-slot pet/animal cargo bag, tSort 254/255/256) --
        // dedicated repository, not added to ICharacterRepository (same "hot magnet file" precedent as
        // IRuneRepository/RuneRepository above). Consumed by PetBagActionService (Fenrir.Application.Game.Services)
        // once IPetBagActionService is registered there. Gated on Database/_manifest.txt registering
        // Migrations/043_character_petbag_table.sql + Migrations/044_character_petbag_procs.sql -- not touched
        // here, that is fenrir-database-engineer's call per this project's own manifest-ownership rule.
        builder.Services.AddSingleton<IPetBagRepository, PetBagRepository>();

        // C8 BigMoney ("1B") family (tSort 241/242/244/245, Inventory<->Store/Save) --
        // Fenrir.Data.Abstractions.Inventory.IBigMoneyRepository, NOT the now-deleted
        // Fenrir.Data.Abstractions.Characters.IBigMoneyRepository duplicate a sibling wave7 pass independently
        // built for the same opcodes: that Characters-namespace version (usp_Character_AdjustBigMoneyStore /
        // usp_AccountVault_TransferBigMoneyWithCharacter [old CREATE PROCEDURE, codes 50350/50351] /
        // usp_Character_AdjustBigMoneyConversion, backed by game.AccountVault.Money2) had zero consumers
        // anywhere in Application/, while this Inventory-namespace version already had a real one
        // (BigMoneyTransferService) plus its own migration-added game.AccountVault.BigMoney column
        // (Migrations/045-046, CREATE OR ALTER, codes 50353/50354) -- see this pass's own integration report
        // for the full collision writeup. Consumed by BigMoneyTransferService (Fenrir.Application.Game.Services)
        // once IBigMoneyTransferService is registered there.
        builder.Services.AddSingleton<IBigMoneyRepository, BigMoneyRepository>();

        // B5 rune-socket durable state (game.CharacterRunes, op157) -- deliberately off ICharacterRepository's
        // hot write-behind path; each rune change is a rare, player-initiated, transactional socket<->inventory swap.
        builder.Services.AddSingleton<IRuneRepository, RuneRepository>();

        // C8-trade durable, anti-dupe two-character trade commit (game.TradeCommitLedger) -- deliberately a
        // dedicated repository, not added to ICharacterRepository/CharacterRepository (same "hot magnet file"
        // precedent as IRuneRepository/RuneRepository above). Registered here so it is resolvable the moment
        // TradeLockService's constructor gains the corresponding parameter and switches CommitAsync over to
        // ExecuteIdempotentAsync -- that Services-layer switch-over (plus the TradeSession.CommitToken field) is
        // out of this pass's scope; see this integration pass's own report. Also gated on
        // Database/_manifest.txt registering Tables/game/TradeCommitLedger.sql and
        // StoredProcedures/game/usp_CharacterTradeCommit_ExecuteIdempotent.sql -- not touched here, that is
        // fenrir-database-engineer's call per this project's own manifest-ownership rule.
        builder.Services.AddSingleton<ITradeCommitRepository, TradeCommitRepository>();

        // C13 War-Point dual-currency NPC-shop purchase (atomic WP debit + item grant, game.usp_Character_BuyWarPointItem).
        builder.Services.AddSingleton<IWarPointRepository, WarPointRepository>();

        builder.Services.AddSingleton<IStarterKitRepository, StarterKitRepository>();
        builder.Services.AddSingleton<ISessionTicketRepository, SessionTicketRepository>();
        builder.Services.AddSingleton<IGameServerDirectoryRepository, GameServerDirectoryRepository>();
        builder.Services.AddSingleton<ICharacterShardLocationRepository, CharacterShardLocationRepository>();

        // D1 logout-info snapshot -- GameServer's EnterWorldService captures via UpsertAsync. Resolved on GameServer
        // only (the login-train sources LogoutInfo from the roster DTO, so LoginService does NOT inject this).
        builder.Services.AddSingleton<ICharacterLogoutStateRepository, CharacterLogoutStateRepository>();
        builder.Services.AddSingleton<IAccountSessionRepository, AccountSessionRepository>();

        // Cross-shard fan-out for GuildAnnouncement/GuildChat/TribeAnnouncement/TribeAnnouncementScroll --
        // consumed by GuildTribeBroadcastRelayHost (Fenrir.Application.Game.Hosting), not by *.Services
        // directly (see IGuildTribeBroadcastRelayQueue's own remarks for that boundary).
        builder.Services.AddSingleton<IGuildTribeBroadcastRelayRepository, GuildTribeBroadcastRelayRepository>();

        // WS1.4 cross-shard social-negotiation relay (Party/Friend/Mentor/Duel/Trade/GuildInvite Ask+Answer)
        // -- point-to-point sibling of the fan-out registration above; consumed by a future
        // SocialCrossShardRelayHost (Fenrir.Application.Game.Hosting), not by *.Services directly.
        builder.Services.AddSingleton<ISocialCrossShardRelayRepository, SocialCrossShardRelayRepository>();

        // WS-C3 cross-shard whisper relay -- consumed only by ChatCrossShardRelayHost
        // (Fenrir.Application.Game.Hosting), never by *.Services directly.
        builder.Services.AddSingleton<IChatCrossShardRelayRepository, ChatCrossShardRelayRepository>();

        // proxy-shop-rental-sync: cross-shard fan-out for the proxy/deputy-shop rental-extension consumables'
        // best-effort live-registry mirror update -- single-purpose sibling of the fan-out registration
        // above; consumed by ProxyShopExpirationRelayHost (Fenrir.Application.Game.Hosting), not by
        // *.Services directly.
        builder.Services.AddSingleton<IProxyShopExpirationRelayRepository, ProxyShopExpirationRelayRepository>();

        // guild-buff-expiry: cross-shard fan-out for the guild-buff-reserve-exhaustion immediate strip-effect
        // push -- single-purpose sibling of the fan-out registration above; consumed by
        // GuildBuffExpiryRelayHost (Fenrir.Application.Game.Hosting), not by *.Services directly.
        builder.Services.AddSingleton<IGuildBuffExpiryRelayRepository, GuildBuffExpiryRelayRepository>();

        // rvr-siege: cross-shard fan-out for the Zone049 siege-zone-slot family (sub-codes 1-9) and the
        // tribe-symbol/alliance family (tSort 38/39/40/42/45/46/47) -- single-purpose sibling of the fan-out
        // registrations above; consumed by RvrSiegeEventRelayHost (Fenrir.Application.Game.Hosting), not by
        // *.Domain/*.Services directly.
        builder.Services.AddSingleton<IRvrSiegeEventRelayRepository, RvrSiegeEventRelayRepository>();

        // D1 party-resync cross-shard fan-out relay -- consumed only by PartyResyncRelayHost, never *.Services directly.
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
        builder.Services.AddSingleton<ApplicationFirewall>();
        builder.Services.AddSingleton<IGuildRepository, GuildRepository>();
        builder.Services.AddSingleton<ITribeRepository, TribeRepository>();
        builder.Services.AddSingleton<ITribeRosterRepository, TribeRosterRepository>();
        builder.Services.AddSingleton<ITribeBankSweepRepository, TribeBankSweepRepository>();
        builder.Services.AddSingleton<ITribeConversionRepository, TribeConversionRepository>();
        builder.Services.AddSingleton<IFourGuildScoringRepository, FourGuildScoringRepository>();
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
