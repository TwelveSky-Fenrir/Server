namespace Fenrir.Application.Game.Domain.Social.Duel;

public enum DuelAskOutcome
{
    Sent,
    ChallengerBusy,
    TargetBusy,

        ChallengerAlreadyDueling
}

public enum DuelEndReason
{
    TimeExpired = 0,
    OpponentDied = 1,
    SelfDied = 2,
    OpponentNotFound = 3
}

public sealed record ActiveDuel(int UniqueNumber, int PlayerA, int PlayerB, bool NoPotions)
{

        public int RemainingTicks { get; set; } = DuelRegistry.DurationTicks;

    public int OpponentOf(int characterId)
    {
        return characterId == PlayerA ? PlayerB : PlayerA;
    }
}

public sealed class DuelRegistry
{

        public const int DurationTicks = 180;

        private readonly Dictionary<int, int> _acceptedPairs = new();

        private readonly Dictionary<int, ActiveDuel> _activeByCharacter = new();

        private readonly CrossShardNegotiationTracker _crossShard = new();

    private readonly Lock _lock = new();

        private readonly Dictionary<int, bool> _noPotionsByChallenger = new();

    private readonly Dictionary<int, int> _pendingByChallenger = new();
    private readonly Dictionary<int, int> _pendingByTarget = new();

    private int _nextUniqueNumber;

        public bool IsNegotiating(int characterId)
    {
        lock (_lock)
        {
            return _pendingByChallenger.ContainsKey(characterId) || _pendingByTarget.ContainsKey(characterId) ||
                   _acceptedPairs.ContainsKey(characterId) || _crossShard.IsPending(characterId);
        }
    }

        public DuelAskOutcome TryAskCrossShard(int challengerId, CrossShardOutboundAsk ask)
    {
        lock (_lock)
        {
            if (IsActivelyDueling(challengerId))
                return DuelAskOutcome.ChallengerAlreadyDueling;
            if (IsNegotiating(challengerId))
                return DuelAskOutcome.ChallengerBusy;

            return _crossShard.TryRegisterOutbound(challengerId, ask)
                ? DuelAskOutcome.Sent
                : DuelAskOutcome.ChallengerBusy;
        }
    }

    public bool IsActivelyDueling(int characterId)
    {
        return _activeByCharacter.ContainsKey(characterId);
    }

    public DuelAskOutcome TryAsk(int challengerId, int targetId, bool noPotions)
    {
        lock (_lock)
        {
            if (IsActivelyDueling(challengerId))
                return DuelAskOutcome.ChallengerAlreadyDueling;
            if (IsNegotiating(challengerId))
                return DuelAskOutcome.ChallengerBusy;
            if (IsNegotiating(targetId) || IsActivelyDueling(targetId))
                return DuelAskOutcome.TargetBusy;

            _pendingByChallenger[challengerId] = targetId;
            _pendingByTarget[targetId] = challengerId;
            _noPotionsByChallenger[challengerId] = noPotions;
            return DuelAskOutcome.Sent;
        }
    }

    public bool TryCancel(int challengerId, out int targetId)
    {
        lock (_lock)
        {
            if (_pendingByChallenger.Remove(challengerId, out targetId))
            {
                _pendingByTarget.Remove(targetId);
                _noPotionsByChallenger.Remove(challengerId);
                return true;
            }

            if (_crossShard.TryConsumeOutbound(challengerId, out var crossShardAsk))
            {
                targetId = crossShardAsk.TargetCharacterId;
                return true;
            }

            return false;
        }
    }

    public bool TryAnswer(int targetId, bool accepted, out int challengerId)
    {
        lock (_lock)
        {
            if (!_pendingByTarget.Remove(targetId, out challengerId))
                return false;

            _pendingByChallenger.Remove(challengerId);

            if (accepted)
            {
                _acceptedPairs[challengerId] = targetId;
                _acceptedPairs[targetId] = challengerId;
            }
            else
            {
                _noPotionsByChallenger.Remove(challengerId);
            }

            return true;
        }
    }

        public bool TryStart(int callerId, out ActiveDuel duel)
    {
        lock (_lock)
        {
            duel = null!;

            if (!_acceptedPairs.Remove(callerId, out var opponentId))
                return false;

            _acceptedPairs.Remove(opponentId);

            var challengerId = _noPotionsByChallenger.ContainsKey(callerId) ? callerId : opponentId;
            _noPotionsByChallenger.Remove(challengerId, out var noPotions);

            duel = new ActiveDuel(++_nextUniqueNumber, callerId, opponentId, noPotions);
            _activeByCharacter[callerId] = duel;
            _activeByCharacter[opponentId] = duel;
            return true;
        }
    }

    public bool TryGetActiveDuel(int characterId, out ActiveDuel? duel)
    {
        lock (_lock)
        {
            return _activeByCharacter.TryGetValue(characterId, out duel);
        }
    }

        public bool TryEndActiveDuel(int characterId, out int opponentId)
    {
        lock (_lock)
        {
            opponentId = 0;

            if (!_activeByCharacter.Remove(characterId, out var duel))
                return false;

            opponentId = duel.OpponentOf(characterId);
            _activeByCharacter.Remove(opponentId);
            return true;
        }
    }

        public void ForceClearOnZoneEntry(int characterId)
    {
        lock (_lock)
        {
            if (_pendingByChallenger.Remove(characterId, out var pendingTarget))
                _pendingByTarget.Remove(pendingTarget);
            if (_pendingByTarget.Remove(characterId, out var pendingChallenger))
                _pendingByChallenger.Remove(pendingChallenger);

            if (_acceptedPairs.Remove(characterId, out var acceptedPartner))
                _acceptedPairs.Remove(acceptedPartner);

            _noPotionsByChallenger.Remove(characterId);

            _activeByCharacter.Remove(characterId);

            _crossShard.TryConsumeOutbound(characterId, out _);
        }
    }
}
