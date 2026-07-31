using Fenrir.Application.Game.Abstractions.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Progression;

public sealed class HeroRankingHandler(IHeroRankingService heroRankingService, ILogger<HeroRankingHandler> logger)
    : IAsyncPacketHandler<HeroRankingRequest>
{
    public async ValueTask HandleAsync(HeroRankingRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;

        logger.LogDebug(
            "Session {SessionId}: HeroRankingRequest (op118) received for character {CharacterId}",
            session.SessionId, characterId);

        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        var result = await heroRankingService.QueryAsync(characterId, zone, state, cancellationToken);

        if (result.Previous is { } previous)
            session.Send(new HeroRankingPreviousResponse { Result = 0, HeroInfo = previous });

        if (result.Current is { } current)
            session.Send(new HeroRankingCurrentResponse { Result = 0, HeroInfo = current });
    }
}
