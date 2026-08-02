using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

public sealed class MentorAskHandler(IMentorAskService mentorAskService, ILogger<MentorAskHandler> logger)
    : IAsyncPacketHandler<MentorRequest>
{
    public async ValueTask HandleAsync(MentorRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;

        logger.LogDebug("MentorAsk: session {SessionId} character {CharacterId} target {TargetAvatarName}",
            session.SessionId, zoneSession.CharacterId, packet.AvatarName);

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var masterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(masterId, out var master) || master is null)
            return;

        var result = await mentorAskService.AskAsync(zone, master, packet.AvatarName, cancellationToken)
            .ConfigureAwait(false);

        switch (result.Kind)
        {
            case MentorAskResultKind.AskerMustDisconnect:
            case MentorAskResultKind.TargetMustDisconnect:
                return;
            case MentorAskResultKind.TargetNotFound:
                session.Send(new MentorAnswerResponse { Answer = 4 });
                return;
            case MentorAskResultKind.AskerBusy:
                session.Send(new MentorAnswerResponse { Answer = 3 });
                return;
            case MentorAskResultKind.TargetBusy:
                session.Send(new MentorAnswerResponse { Answer = 5 });
                return;
            case MentorAskResultKind.TargetAlreadyHasTeacher:
                session.Send(new MentorAnswerResponse { Answer = 6 });
                return;
            case MentorAskResultKind.TargetAlreadyHasStudent:
                session.Send(new MentorAnswerResponse { Answer = 7 });
                return;
            case MentorAskResultKind.Sent:
                zone.TryGetPlayer(result.TargetCharacterId, out var student);
                student!.Session.Send(new MentorResponse { AvatarName = result.AskerName! });
                return;
            case MentorAskResultKind.SentCrossShard:
                return;
        }
    }
}
