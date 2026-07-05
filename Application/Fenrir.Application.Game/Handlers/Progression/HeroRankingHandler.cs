using Fenrir.Application.Game.Handlers.Progression.Services;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Progression;

/// <summary>
///     CZ_HERORANK_INFO_SEND (opcode 118). The legacy gates each reply on whether the server-wide ranking
///     snapshot (refreshed by a periodic DB-poll job) has advanced by more than 2.5s beyond what this
///     connection last saw, independently for the Previous and Current periods
///     (S04_MyWork02.cpp:14159-14176). Fenrir has no separate ranking-refresh job, so this reproduces the
///     same observable cadence as a flat per-connection 2.5s throttle instead, always querying live.
/// </summary>
public sealed class HeroRankingHandler(IHeroRankingService heroRankingService)
    : IAsyncPacketHandler<HeroRankingRequest>
{
    public async ValueTask HandleAsync(HeroRankingRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        var result = await heroRankingService.QueryAsync(characterId, zone, state, cancellationToken);

        if (result.Previous is { } previous)
            session.Send(new HeroRankingPreviousResponse { Result = 0, HeroInfo = previous });

        if (result.Current is { } current)
            session.Send(new HeroRankingCurrentResponse { Result = 0, HeroInfo = current });
    }
}
