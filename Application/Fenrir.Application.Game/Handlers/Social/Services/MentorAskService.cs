using Fenrir.Application.Game.Social.Mentor;
using Fenrir.Application.Game.World;

namespace Fenrir.Application.Game.Handlers.Social.Services;

/// <summary>Discriminator for how a CZ_TEACHER_ASK_SEND attempt resolved.</summary>
public enum MentorAskResultKind
{
    AskerMustDisconnect,
    TargetNotFound,
    TargetMustDisconnect,
    AskerBusy,
    TargetBusy,
    TargetAlreadyHasTeacher,
    TargetAlreadyHasStudent,
    Sent
}

public readonly record struct MentorAskResult(
    MentorAskResultKind Kind,
    int TargetCharacterId = 0,
    string? TargetName = null,
    string? AskerName = null);

public interface IMentorAskService
{
    MentorAskResult Ask(Zone zone, PlayerRuntimeState master, string targetAvatarName);
}

public sealed class MentorAskService(MentorRegistry mentors) : IMentorAskService
{
    private const int MinimumMasterLevel = 113;

    public MentorAskResult Ask(Zone zone, PlayerRuntimeState master, string targetAvatarName)
    {
        if (master.Level < MinimumMasterLevel || master.TeacherCharacterId is not null ||
            master.StudentCharacterId is not null)
            return new MentorAskResult(MentorAskResultKind.AskerMustDisconnect);

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
            MentorAskOutcome.TargetAlreadyHasTeacher => new MentorAskResult(MentorAskResultKind.TargetAlreadyHasTeacher),
            MentorAskOutcome.TargetAlreadyHasStudent => new MentorAskResult(MentorAskResultKind.TargetAlreadyHasStudent),
            _ => new MentorAskResult(MentorAskResultKind.Sent, student.CharacterId, student.Name, master.Name)
        };
    }
}
