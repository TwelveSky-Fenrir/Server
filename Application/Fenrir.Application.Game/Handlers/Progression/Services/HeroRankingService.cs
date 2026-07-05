using Fenrir.Application.Game.Progression;
using Fenrir.Application.Game.World;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Data.Progression;

namespace Fenrir.Application.Game.Handlers.Progression.Services;

/// <summary><see langword="null" /> per period means that period's throttle has not elapsed yet -- no reply for it.</summary>
public readonly record struct HeroRankingQueryResult(HeroRank? Previous, HeroRank? Current);

public interface IHeroRankingService
{
    ValueTask<HeroRankingQueryResult> QueryAsync(int characterId, Zone zone, PlayerRuntimeState state,
        CancellationToken cancellationToken);
}

/// <summary>
///     Business logic extracted from <c>HeroRankingHandler</c> (CZ_HERORANK_INFO_SEND, opcode 118). See that
///     handler's own remarks for why a flat per-connection 2.5s throttle stands in for the legacy's
///     server-wide ranking-refresh cadence.
/// </summary>
public sealed class HeroRankingService(IHeroRankingRepository heroRankings) : IHeroRankingService
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

        if (state.LastHeroRankingCurrentQueryAtZoneClock is not { } lastCurrent ||
            now - lastCurrent > ThrottleInterval)
        {
            var rows = await heroRankings.GetByPeriodAsync(0, cancellationToken);
            current = HeroRankBuilder.Build(rows);
            zone.PostHeroRankingQueryCommand(new HeroRankingQueryZoneCommand(characterId, false, now));
        }

        return new HeroRankingQueryResult(previous, current);
    }
}
