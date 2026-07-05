namespace Fenrir.Application.Game.Domain.Social.Mentor;

/// <summary>Soft outcomes of CZ_TEACHER_ASK_SEND -- mirrors ZC_TEACHER_ANSWER_RECV's pre-check codes.</summary>
public enum MentorAskOutcome
{
    Sent,
    AskerBusy, // 3
    TargetBusy, // 5
    TargetAlreadyHasTeacher, // 6
    TargetAlreadyHasStudent // 7
}

/// <summary>
///     Process-wide teacher/student ("mentor") negotiation authority (named Mentor to avoid colliding with
///     the wire's own Teacher/Student AvatarInfo properties). The durable bond lives in
///     game.Characters.TeacherCharacterId/StudentCharacterId; this registry only tracks the
///     ask/cancel/answer handshake. Unlike Friend, CZ_TEACHER_START_SEND is a single action taken by the
///     master that bonds both sides at once, so the accepted state is remembered keyed by master only.
/// </summary>
public sealed class MentorRegistry
{
    /// <summary>master characterId -> accepted student characterId, awaiting CZ_TEACHER_START_SEND.</summary>
    private readonly Dictionary<int, int> _acceptedByMaster = new();

    private readonly Lock _lock = new();
    private readonly Dictionary<int, int> _pendingByMaster = new();
    private readonly Dictionary<int, int> _pendingByStudent = new();

    private bool IsNegotiating(int characterId)
    {
        return _pendingByMaster.ContainsKey(characterId) || _pendingByStudent.ContainsKey(characterId) ||
               _acceptedByMaster.ContainsKey(characterId) || _acceptedByMaster.ContainsValue(characterId);
    }

    public MentorAskOutcome TryAsk(int masterId, int studentId, bool targetAlreadyHasTeacher,
        bool targetAlreadyHasStudent)
    {
        lock (_lock)
        {
            if (IsNegotiating(masterId))
                return MentorAskOutcome.AskerBusy;
            if (IsNegotiating(studentId))
                return MentorAskOutcome.TargetBusy;
            if (targetAlreadyHasTeacher)
                return MentorAskOutcome.TargetAlreadyHasTeacher;
            if (targetAlreadyHasStudent)
                return MentorAskOutcome.TargetAlreadyHasStudent;

            _pendingByMaster[masterId] = studentId;
            _pendingByStudent[studentId] = masterId;
            return MentorAskOutcome.Sent;
        }
    }

    public bool TryCancel(int masterId, out int studentId)
    {
        lock (_lock)
        {
            if (!_pendingByMaster.Remove(masterId, out studentId))
                return false;

            _pendingByStudent.Remove(studentId);
            return true;
        }
    }

    public bool TryAnswer(int studentId, bool accepted, out int masterId)
    {
        lock (_lock)
        {
            if (!_pendingByStudent.Remove(studentId, out masterId))
                return false;

            _pendingByMaster.Remove(masterId);

            if (accepted)
                _acceptedByMaster[masterId] = studentId;

            return true;
        }
    }

    /// <summary>CZ_TEACHER_START_SEND -- consumes the accepted negotiation; only the master may call this.</summary>
    public bool TryConsumeStart(int masterId, out int studentId)
    {
        lock (_lock)
        {
            return _acceptedByMaster.Remove(masterId, out studentId);
        }
    }
}
