namespace Fenrir.Application.Game.Domain.Social.Mentor;

public enum MentorAskOutcome
{
    Sent,
    AskerBusy,
    TargetBusy,
    TargetAlreadyHasTeacher,
    TargetAlreadyHasStudent
}

public sealed class MentorRegistry
{
    private readonly Dictionary<int, int> _acceptedByMaster = new();

    private readonly Dictionary<int, int> _acceptedByStudent = new();

    private readonly Lock _lock = new();
    private readonly Dictionary<int, int> _pendingByMaster = new();
    private readonly Dictionary<int, int> _pendingByStudent = new();

    public bool IsNegotiating(int characterId)
    {
        lock (_lock)
        {
            return _pendingByMaster.ContainsKey(characterId) || _pendingByStudent.ContainsKey(characterId) ||
                   _acceptedByMaster.ContainsKey(characterId) || _acceptedByStudent.ContainsKey(characterId);
        }
    }

    public bool TryPeekPending(int characterId, out int counterpartId, out bool isMaster)
    {
        lock (_lock)
        {
            if (_pendingByMaster.TryGetValue(characterId, out counterpartId))
            {
                isMaster = true;
                return true;
            }

            if (_pendingByStudent.TryGetValue(characterId, out counterpartId))
            {
                isMaster = false;
                return true;
            }

            isMaster = false;
            return false;
        }
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
            if (_pendingByMaster.Remove(masterId, out studentId))
            {
                _pendingByStudent.Remove(studentId);
                return true;
            }

            return false;
        }
    }

    public bool TryWithdrawAsk(int masterId, out int studentId)
    {
        lock (_lock)
        {
            return _pendingByMaster.Remove(masterId, out studentId);
        }
    }

    public bool TryAcknowledgeWithdrawal(int studentId, int expectedMasterId)
    {
        lock (_lock)
        {
            if (_pendingByStudent.TryGetValue(studentId, out var recordedMasterId) &&
                recordedMasterId == expectedMasterId)
            {
                _pendingByStudent.Remove(studentId);
                return true;
            }

            return false;
        }
    }

    public bool TryAnswer(int studentId, bool accepted, bool masterBusyByZoneTransfer, out int masterId,
        out bool guardBlocked)
    {
        guardBlocked = false;

        lock (_lock)
        {
            if (!_pendingByStudent.TryGetValue(studentId, out masterId))
                return false;

            if (!_pendingByMaster.TryGetValue(masterId, out var recordedStudentId) || recordedStudentId != studentId)
            {
                _pendingByStudent.Remove(studentId);
                return false;
            }

            if (masterBusyByZoneTransfer)
            {
                // A decline always releases the answering side, matching every sibling registry: the student has no
                // cancel opcode, and the sweep never fires once the master's transfer completes.
                if (!accepted)
                    _pendingByStudent.Remove(studentId);

                guardBlocked = true;
                return false;
            }

            _pendingByStudent.Remove(studentId);
            _pendingByMaster.Remove(masterId);

            if (accepted)
            {
                _acceptedByStudent[studentId] = masterId;
                _acceptedByMaster[masterId] = studentId;
            }

            return true;
        }
    }

    public bool TryConsumeStart(int masterId, out int studentId)
    {
        lock (_lock)
        {
            return _acceptedByMaster.Remove(masterId, out studentId);
        }
    }

    public bool TryAcknowledgeStart(int studentId, int expectedMasterId)
    {
        lock (_lock)
        {
            if (_acceptedByStudent.TryGetValue(studentId, out var recordedMasterId) &&
                recordedMasterId == expectedMasterId)
            {
                _acceptedByStudent.Remove(studentId);
                return true;
            }

            return false;
        }
    }

    public bool TryClearAcceptedForDisconnect(int characterId, out int counterpartId)
    {
        lock (_lock)
        {
            if (_acceptedByMaster.Remove(characterId, out counterpartId))
            {
                _acceptedByStudent.Remove(counterpartId);
                return true;
            }

            if (_acceptedByStudent.Remove(characterId, out counterpartId))
            {
                _acceptedByMaster.Remove(counterpartId);
                return true;
            }

            return false;
        }
    }

    public void ClearForWorldEntry(int characterId)
    {
        lock (_lock)
        {
            if (_pendingByMaster.Remove(characterId, out var pendingStudent))
                RemoveMirror(_pendingByStudent, pendingStudent, characterId);
            if (_pendingByStudent.Remove(characterId, out var pendingMaster))
                RemoveMirror(_pendingByMaster, pendingMaster, characterId);

            if (_acceptedByMaster.Remove(characterId, out var acceptedStudent))
                RemoveMirror(_acceptedByStudent, acceptedStudent, characterId);
            if (_acceptedByStudent.Remove(characterId, out var acceptedMaster))
                RemoveMirror(_acceptedByMaster, acceptedMaster, characterId);
        }
    }

    private static void RemoveMirror(Dictionary<int, int> map, int counterpartId, int expectedValue)
    {
        if (map.TryGetValue(counterpartId, out var mirror) && mirror == expectedValue)
            map.Remove(counterpartId);
    }
}
