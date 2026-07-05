using Fenrir.Application.Game.Progression;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Progression;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Progression;

/// <summary>
///     CZ_HERORANK_INFO_SEND (opcode 118). The legacy gates each reply on whether the server-wide ranking
///     snapshot (refreshed by a periodic DB-poll job) has advanced by more than 2.5s beyond what this
///     connection last saw, independently for the Previous and Current periods
///     (S04_MyWork02.cpp:14159-14176). Fenrir has no separate ranking-refresh job, so this reproduces the
///     same observable cadence as a flat per-connection 2.5s throttle instead, always querying live.
/// </summary>
public sealed class HeroRankingHandler(IHeroRankingRepository heroRankings) : IAsyncPacketHandler<HeroRankingRequest>
{
    private static readonly TimeSpan ThrottleInterval = TimeSpan.FromMilliseconds(2500);

    public async ValueTask HandleAsync(HeroRankingRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        var now = TimeSpan.FromMilliseconds(Environment.TickCount64);

        if (state.LastHeroRankingPreviousQueryAtZoneClock is not { } lastPrevious ||
            now - lastPrevious > ThrottleInterval)
        {
            var rows = await heroRankings.GetByPeriodAsync(1, cancellationToken);
            session.Send(new HeroRankingPreviousResponse { Result = 0, HeroInfo = HeroRankBuilder.Build(rows) });
            zone.PostHeroRankingQueryCommand(new HeroRankingQueryZoneCommand(characterId, true, now));
        }

        if (state.LastHeroRankingCurrentQueryAtZoneClock is not { } lastCurrent ||
            now - lastCurrent > ThrottleInterval)
        {
            var rows = await heroRankings.GetByPeriodAsync(0, cancellationToken);
            session.Send(new HeroRankingCurrentResponse { Result = 0, HeroInfo = HeroRankBuilder.Build(rows) });
            zone.PostHeroRankingQueryCommand(new HeroRankingQueryZoneCommand(characterId, false, now));
        }
    }
}
