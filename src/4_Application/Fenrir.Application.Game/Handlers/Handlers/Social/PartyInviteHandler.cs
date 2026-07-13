using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Application.Game;
using Fenrir.Application.Game.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

public sealed class PartyInviteHandler(IPartyInviteService partyInviteService, ILogger<PartyInviteHandler> logger)
    : IAsyncPacketHandler<PartyInviteRequest>
{
    public async ValueTask HandleAsync(PartyInviteRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;

        logger.LogDebug(
            "PartyInvite: session {SessionId} character {CharacterId} target {TargetAvatarName}",
            session.SessionId, zoneSession.CharacterId, packet.AvatarName);

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var inviterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(inviterId, out var inviter) || inviter is null)
            return;

        var result = await partyInviteService.InviteAsync(zone, inviter, packet.AvatarName, cancellationToken)
            .ConfigureAwait(false);

        switch (result.Kind)
        {
            case PartyInviteResultKind.InviterMustDisconnect:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            case PartyInviteResultKind.TargetNotFound:
                session.Send(new PartyAnswerResponse { Answer = 4 });
                return;
            case PartyInviteResultKind.InviterBusy:
                session.Send(new PartyAnswerResponse { Answer = 3 });
                return;
            case PartyInviteResultKind.TargetBusy:
                session.Send(new PartyAnswerResponse { Answer = 5 });
                return;
            case PartyInviteResultKind.TargetAlreadyPartied:
                session.Send(new PartyAnswerResponse { Answer = 6 });
                return;
            case PartyInviteResultKind.Sent:
                zone.TryGetPlayer(result.TargetCharacterId, out var target);
                target!.Session.Send(new PartyInviteResponse { AvatarName = result.InviterName! });
                return;
            case PartyInviteResultKind.SentCrossShard:
                return;
        }
    }
}
