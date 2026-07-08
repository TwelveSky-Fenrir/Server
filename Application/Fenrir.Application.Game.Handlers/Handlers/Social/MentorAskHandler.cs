using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

/// <summary>CZ_TEACHER_ASK_SEND (opcode 59) -- sender becomes master (MG5ORIGIN branch, active in this build).</summary>
public sealed class MentorAskHandler(IMentorAskService mentorAskService, ILogger<MentorAskHandler> logger)
    : IInlinePacketHandler<MentorRequest>
{
    public void Handle(in MentorRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        logger.LogDebug("MentorAsk: session {SessionId} character {CharacterId} target {TargetAvatarName}",
            session.SessionId, zoneSession.CharacterId, packet.AvatarName);

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var masterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(masterId, out var master) || master is null)
            return;

        var result = mentorAskService.Ask(zone, master, packet.AvatarName);

        switch (result.Kind)
        {
            case MentorAskResultKind.AskerMustDisconnect:
            case MentorAskResultKind.TargetMustDisconnect:
                zoneSession.Abort(DisconnectReason.Faulted);
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
        }
    }
}
