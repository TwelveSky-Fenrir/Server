using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Movement;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Data.WriteBehind;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Domain.Extensions;

/// <summary>
///     Process-wide Domain singletons: the simulation-system pipeline, cross-zone registries, and
///     <see cref="GameServerOptions" /> binding. Relocated here (unchanged) from Fenrir.GameServer's
///     Program.cs during the project split.
/// </summary>
public static class DomainServiceCollectionExtensions
{
    /// <summary>
    ///     Does NOT bind <see cref="GameServerOptions" /> to configuration itself — that call needs the
    ///     AOT-safe configuration-binding source generator, which is only enabled on FenrirExecutable projects
    ///     (Directory.Build.targets), not this shared class library. The executable's own Program.cs calls
    ///     "services.Configure&lt;GameServerOptions&gt;(configuration.GetSection("Game"))" directly before this method.
    /// </summary>
    public static IServiceCollection AddGameDomain(this IServiceCollection services)
    {
        services.AddSingleton<IValidateOptions<GameServerOptions>, GameServerOptionsValidator>();
        services.AddOptions<GameServerOptions>().ValidateOnStart();

        services.AddSingleton<MovementRules>();
        services.AddSingleton<DirtyTracker<int>>();

        services.AddSingleton<QuestCatalog>();
        services.AddSingleton<KillCooldownTracker>(); // C05 anti-farm gate, shared by every Zone via ZoneRegistry

        // Registration order IS simulation order within a zone's tick: buffs must expire before meditation regen
        // reads a (possibly just-cleared) sit-skill, and before auto-hunt decides which configured buff is still
        // active; monster AI runs before that tick's respawn scan.
        services.AddSingleton<ISimulationSystem, BuffExpirySystem>();
        services.AddSingleton<ISimulationSystem, AutoHuntTickSystem>();
        services.AddSingleton<ISimulationSystem, MeditationRegenSystem>();
        services.AddSingleton<ISimulationSystem, MonsterAiSystem>();
        services.AddSingleton<ISimulationSystem, MonsterSpawnScheduler>();
        services.AddSingleton<ISimulationSystem, TowerGuardianSystem>();
        services.AddSingleton<ISimulationSystem, PetActivitySystem>();

        services.AddSingleton<ZoneRegistry>();

        // Process-wide singletons: a party/duel/trade/friend-ask/mentor-ask negotiation can span multiple Zone actors.
        services.AddSingleton<PartyRegistry>();
        services.AddSingleton<FriendRegistry>();
        services.AddSingleton<MentorRegistry>();
        services.AddSingleton<DuelRegistry>();
        services.AddSingleton<TradeRegistry>();
        services.AddSingleton<GuildInviteRegistry>();
        services.AddSingleton<TowerWarState>();

        // C08: RvR ranking-board cache (GuildRankingCache.Top is read synchronously by EnterWorldHandler/
        // ZoneMoveHandler); kept warm by a periodic refresh registered in Fenrir.Application.Game.Hosting.
        services.AddSingleton<GuildRankingCache>();

        return services;
    }
}
