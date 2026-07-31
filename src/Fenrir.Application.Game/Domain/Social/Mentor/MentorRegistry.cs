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

    private readonly CrossShardNegotiationTracker _crossShard = new();

    private readonly Lock _lock = new();
    private readonly Dictionary<int, int> _pendingByMaster = new();
    private readonly Dictionary<int, int> _pendingByStudent = new();

    public bool IsNegotiating(int characterId)
    {
        lock (_lock)
        {
            return _pendingByMaster.ContainsKey(characterId) || _pendingByStudent.ContainsKey(characterId) ||
                   _acceptedByMaster.ContainsKey(characterId) || _acceptedByStudent.ContainsKey(characterId) ||
                   _crossShard.IsPending(characterId);
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

    public MentorAskOutcome TryAskCrossShard(int masterId, CrossShardOutboundAsk ask)
    {
        lock (_lock)
        {
            if (IsNegotiating(masterId))
                return MentorAskOutcome.AskerBusy;

            return _crossShard.TryRegisterOutbound(masterId, ask) ? MentorAskOutcome.Sent : MentorAskOutcome.AskerBusy;
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

            if (_crossShard.TryConsumeOutbound(masterId, out var crossShardAsk))
            {
                studentId = crossShardAsk.TargetCharacterId;
                return true;
            }

            return false;
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
            {
                _acceptedByMaster[masterId] = studentId;
                _acceptedByStudent[studentId] = masterId;
            }

            return true;
        }
    }

    public bool TryConsumeStart(int masterId, out int studentId)
    {
        lock (_lock)
        {
            if (!_acceptedByMaster.Remove(masterId, out studentId))
                return false;

            _acceptedByStudent.Remove(studentId);
            return true;
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
}
