using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

public sealed class MentorStatusHandler(IMentorStatusService mentorStatusService, ILogger<MentorStatusHandler> logger)
    : IInlinePacketHandler<MentorStatusRequest>
{
    public void Handle(in MentorStatusRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;

        logger.LogDebug("MentorStatus: session {SessionId} character {CharacterId}", session.SessionId,
            zoneSession.CharacterId);

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        var result = mentorStatusService.GetStatus(zone, state);

        switch (result.Kind)
        {
            case MentorStatusResultKind.NoPartner:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            case MentorStatusResultKind.PartnerNotInZone:
                return;
            case MentorStatusResultKind.Resolved:
                session.Send(new MentorStatusResponse { Result = result.Result });
                return;
        }
    }
}
