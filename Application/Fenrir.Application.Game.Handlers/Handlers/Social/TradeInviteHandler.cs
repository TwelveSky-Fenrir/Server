using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

public sealed class TradeInviteHandler(ITradeInviteService tradeInviteService, ILogger<TradeInviteHandler> logger)
    : IAsyncPacketHandler<TradeInviteRequest>
{
    public async ValueTask HandleAsync(TradeInviteRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;

        logger.LogDebug("TradeInvite: session {SessionId} character {CharacterId} target {TargetAvatarName}",
            session.SessionId, zoneSession.CharacterId, packet.AvatarName);

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var askerId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(askerId, out var asker) || asker is null)
            return;

        var result = await tradeInviteService.InviteAsync(zone, asker, packet.AvatarName, cancellationToken)
            .ConfigureAwait(false);

        switch (result.Kind)
        {
            case TradeInviteResultKind.TargetNotFound:
                session.Send(new TradeAnswerResponse { Answer = 4 });
                return;
            case TradeInviteResultKind.MustDisconnect:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            case TradeInviteResultKind.AskerBusy:
                session.Send(new TradeAnswerResponse { Answer = 3 });
                return;
            case TradeInviteResultKind.TargetBusy:
                session.Send(new TradeAnswerResponse { Answer = 5 });
                return;
            case TradeInviteResultKind.Sent:
                zone.TryGetPlayer(result.TargetCharacterId, out var target);
                target!.Session.Send(new TradeInviteResponse
                    { AvatarName = result.AskerName!, Level = result.AskerLevel });
                return;
            case TradeInviteResultKind.SentCrossShard:
                return;
        }
    }
}
