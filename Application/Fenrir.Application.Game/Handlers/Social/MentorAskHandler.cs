using Fenrir.Application.Game.Social.Mentor;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_TEACHER_ASK_SEND (opcode 59). Sender becomes the future MASTER: requires level ≥ 113 and not
///     already a teacher/student (else Quit()). Target must be same tribe and strictly lower level (else
///     Quit() -- the MG5ORIGIN branch, active in this build), resolved within the asker's own zone only.
/// </summary>
public sealed class MentorAskHandler(MentorRegistry mentors) : IInlinePacketHandler<MentorRequest>
{
    private const int MinimumMasterLevel = 113;

    public void Handle(in MentorRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var masterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(masterId, out var master) || master is null)
            return;

        if (master.Level < MinimumMasterLevel || master.TeacherCharacterId is not null ||
            master.StudentCharacterId is not null)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        PlayerRuntimeState? student = null;
        foreach (var candidate in zone.Players)
            if (string.Equals(candidate.Name, packet.AvatarName, StringComparison.OrdinalIgnoreCase))
            {
                student = candidate;
                break;
            }

        if (student is null)
        {
            session.Send(new MentorAnswerResponse { Answer = 4 });
            return;
        }

        if (student.Tribe != master.Tribe || student.Level >= master.Level)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var outcome = mentors.TryAsk(masterId, student.CharacterId, student.TeacherCharacterId is not null,
            student.StudentCharacterId is not null);

        switch (outcome)
        {
            case MentorAskOutcome.AskerBusy:
                session.Send(new MentorAnswerResponse { Answer = 3 });
                return;
            case MentorAskOutcome.TargetBusy:
                session.Send(new MentorAnswerResponse { Answer = 5 });
                return;
            case MentorAskOutcome.TargetAlreadyHasTeacher:
                session.Send(new MentorAnswerResponse { Answer = 6 });
                return;
            case MentorAskOutcome.TargetAlreadyHasStudent:
                session.Send(new MentorAnswerResponse { Answer = 7 });
                return;
            case MentorAskOutcome.Sent:
                student.Session.Send(new MentorResponse { AvatarName = master.Name });
                return;
        }
    }
}
