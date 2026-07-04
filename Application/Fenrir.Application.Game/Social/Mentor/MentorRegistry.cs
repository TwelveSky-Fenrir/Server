namespace Fenrir.Application.Game.Social.Mentor;

/// <summary>Soft outcomes of CZ_TEACHER_ASK_SEND -- mirrors ZC_TEACHER_ANSWER_RECV's pre-check codes (contracts/05_social.md: 3 soi occupé, 4 introuvable [handler-resolved], 5 cible occupée, 6 cible a déjà un maître, 7 cible a déjà un élève).</summary>
public enum MentorAskOutcome
{
    Sent,
    AskerBusy, // 3
    TargetBusy, // 5
    TargetAlreadyHasTeacher, // 6
    TargetAlreadyHasStudent // 7
}

/// <summary>
///     Process-wide teacher/student ("mentor") negotiation authority (CZ_TEACHER_* family --
///     Fenrir's already-established naming is <c>Mentor</c>, Opcodes.Zone.Incoming/Outgoing.Mentor*, to
///     avoid colliding with the .NET <c>Teacher</c>/<c>Student</c> wire property names on
///     <c>AvatarInfo</c>). The durable bond itself lives in game.Characters.TeacherCharacterId/
///     StudentCharacterId (<c>MentorRepository</c>) -- this registry only tracks the ask/cancel/answer
///     handshake, exactly like <c>Friends.FriendRegistry</c>. Unlike Friend (where each side separately
///     "makes" the bond), CZ_TEACHER_START_SEND is a SINGLE action taken by the MASTER (the original
///     asker -- contracts/05_social.md: "l'émetteur est le futur MAÎTRE") that bonds both sides in one
///     shot, so the accepted-but-not-yet-started state only needs to be remembered once, keyed by the
///     master.
/// </summary>
public sealed class MentorRegistry
{
    private readonly Lock _lock = new();
    private readonly Dictionary<int, int> _pendingByMaster = new();
    private readonly Dictionary<int, int> _pendingByStudent = new();

    /// <summary>master characterId -&gt; accepted student characterId, awaiting CZ_TEACHER_START_SEND (legacy state 3).</summary>
    private readonly Dictionary<int, int> _acceptedByMaster = new();

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
            if (targetAlreadyHasTeacher)
                return MentorAskOutcome.TargetAlreadyHasTeacher;
            if (targetAlreadyHasStudent)
                return MentorAskOutcome.TargetAlreadyHasStudent;
            if (IsNegotiating(studentId))
                return MentorAskOutcome.TargetBusy;

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
