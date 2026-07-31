using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

public sealed class MentorCancelHandler(
    ZoneRegistry zones,
    IMentorCancelService mentorCancelService,
    ILogger<MentorCancelHandler> logger) : IInlinePacketHandler<MentorCancelRequest>
{
    public void Handle(in MentorCancelRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;

        logger.LogDebug("MentorCancel: session {SessionId} character {CharacterId}", session.SessionId,
            zoneSession.CharacterId);

        var masterId = zoneSession.CharacterId!.Value;

        var result = mentorCancelService.Cancel(masterId);
        if (!result.Handled)
            return;

        if (zones.TryGetPlayer(result.StudentId, out var student))
            student.Session.Send(new MentorCancelResponse());
    }
}
