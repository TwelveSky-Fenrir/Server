using Fenrir.Application.Game.World;

namespace Fenrir.Application.Game.Handlers.Social.Services;

/// <summary>Discriminator for how a CZ_TEACHER_STATE_SEND query resolved.</summary>
public enum MentorStatusResultKind
{
    NoPartner,
    PartnerNotInZone,
    Resolved
}

public readonly record struct MentorStatusResult(MentorStatusResultKind Kind, int Result = 0);

public interface IMentorStatusService
{
    MentorStatusResult GetStatus(Zone zone, PlayerRuntimeState state);
}

/// <summary>
///     Open issue: a character with both a teacher and a student prefers the teacher-side check here, not
///     fully re-verified against source.
/// </summary>
public sealed class MentorStatusService : IMentorStatusService
{
    public MentorStatusResult GetStatus(Zone zone, PlayerRuntimeState state)
    {
        var iAmTheStudent = state.TeacherCharacterId is not null;
        var partnerId = iAmTheStudent ? state.TeacherCharacterId!.Value : state.StudentCharacterId;

        if (partnerId is null)
            return new MentorStatusResult(MentorStatusResultKind.NoPartner);

        if (!zone.TryGetPlayer(partnerId.Value, out var partner) || partner is null)
            return new MentorStatusResult(MentorStatusResultKind.PartnerNotInZone); // partner not in this same zone -- no reply

        var reciprocal = iAmTheStudent
            ? partner.StudentCharacterId == state.CharacterId
            : partner.TeacherCharacterId == state.CharacterId;

        return new MentorStatusResult(MentorStatusResultKind.Resolved, reciprocal ? 0 : 1);
    }
}
