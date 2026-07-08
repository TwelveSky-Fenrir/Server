using Fenrir.Application.Game.Abstractions.Progression;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Shared;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Progression;

/// <summary>
///     Business logic extracted from <c>HeroRankingHandler</c> (CZ_HERORANK_INFO_SEND, opcode 118). See that
///     handler's own remarks for why a flat per-connection 2.5s throttle stands in for the legacy's
///     server-wide ranking-refresh cadence.
/// </summary>
public sealed class HeroRankingService(IHeroRankingRepository heroRankings, ILogger<HeroRankingService> logger)
    : IHeroRankingService
{
    private static readonly TimeSpan ThrottleInterval = TimeSpan.FromMilliseconds(2500);

    public async ValueTask<HeroRankingQueryResult> QueryAsync(int characterId, Zone zone, PlayerRuntimeState state,
        CancellationToken cancellationToken)
    {
        var now = TimeSpan.FromMilliseconds(Environment.TickCount64);

        HeroRank? previous = null;
        HeroRank? current = null;

        if (state.LastHeroRankingPreviousQueryAtZoneClock is not { } lastPrevious ||
            now - lastPrevious > ThrottleInterval)
        {
            var rows = await heroRankings.GetByPeriodAsync(1, cancellationToken);
            previous = HeroRankBuilder.Build(rows);
            zone.PostHeroRankingQueryCommand(new HeroRankingQueryZoneCommand(characterId, true, now));
        }
        else
        {
            logger.LogDebug(
                "Hero-ranking previous-period query throttled for character {CharacterId}: last queried {ElapsedMs:F0}ms ago",
                characterId, (now - lastPrevious).TotalMilliseconds);
        }

        if (state.LastHeroRankingCurrentQueryAtZoneClock is not { } lastCurrent ||
            now - lastCurrent > ThrottleInterval)
        {
            var rows = await heroRankings.GetByPeriodAsync(0, cancellationToken);
            current = HeroRankBuilder.Build(rows);
            zone.PostHeroRankingQueryCommand(new HeroRankingQueryZoneCommand(characterId, false, now));
        }
        else
        {
            logger.LogDebug(
                "Hero-ranking current-period query throttled for character {CharacterId}: last queried {ElapsedMs:F0}ms ago",
                characterId, (now - lastCurrent).TotalMilliseconds);
        }

        return new HeroRankingQueryResult(previous, current);
    }
}
