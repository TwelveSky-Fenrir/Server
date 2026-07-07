using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Services.Social;

public sealed class MentorAskService(MentorRegistry mentors) : IMentorAskService
{
    private const int MinimumMasterLevel = 113;

    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:8259-8277,8459-8471,9088-9101,9311-9324 (the shared
    ///     CZ_DUEL_ASK_SEND/CZ_FRIEND_ASK_SEND/CZ_PARTY_ASK_SEND/CZ_TEACHER_ASK_SEND/CZ_TRADE_ASK_SEND
    ///     pre-check family) -- legacy checks the requester's OWN busy/pose state before it ever resolves
    ///     the target avatar by name. The level/existing-teacher/existing-student disconnect gate already
    ///     ran before target resolution here; the still-negotiating soft-busy check
    ///     (<see cref="MentorRegistry.IsNegotiating" />) did not, and is moved up to join it so a busy
    ///     master naming a nonexistent/offline student gets the busy reply, not "target not found". The
    ///     same check inside <see cref="MentorRegistry.TryAsk" /> stays in place for the actual registration.
    /// </remarks>
    public MentorAskResult Ask(Zone zone, PlayerRuntimeState master, string targetAvatarName)
    {
        if (master.Level < MinimumMasterLevel || master.TeacherCharacterId is not null ||
            master.StudentCharacterId is not null)
            return new MentorAskResult(MentorAskResultKind.AskerMustDisconnect);

        if (mentors.IsNegotiating(master.CharacterId))
            return new MentorAskResult(MentorAskResultKind.AskerBusy);

        PlayerRuntimeState? student = null;
        foreach (var candidate in zone.Players)
            if (string.Equals(candidate.Name, targetAvatarName, StringComparison.OrdinalIgnoreCase))
            {
                student = candidate;
                break;
            }

        if (student is null)
            return new MentorAskResult(MentorAskResultKind.TargetNotFound);

        if (student.Tribe != master.Tribe || student.Level >= master.Level)
            return new MentorAskResult(MentorAskResultKind.TargetMustDisconnect);

        var outcome = mentors.TryAsk(master.CharacterId, student.CharacterId, student.TeacherCharacterId is not null,
            student.StudentCharacterId is not null);

        return outcome switch
        {
            MentorAskOutcome.AskerBusy => new MentorAskResult(MentorAskResultKind.AskerBusy),
            MentorAskOutcome.TargetBusy => new MentorAskResult(MentorAskResultKind.TargetBusy),
            MentorAskOutcome.TargetAlreadyHasTeacher =>
                new MentorAskResult(MentorAskResultKind.TargetAlreadyHasTeacher),
            MentorAskOutcome.TargetAlreadyHasStudent =>
                new MentorAskResult(MentorAskResultKind.TargetAlreadyHasStudent),
            _ => new MentorAskResult(MentorAskResultKind.Sent, student.CharacterId, student.Name, master.Name)
        };
    }
}
