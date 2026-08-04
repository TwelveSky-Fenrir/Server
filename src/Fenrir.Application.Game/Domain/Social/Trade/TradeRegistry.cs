namespace Fenrir.Application.Game.Domain.Social.Trade;

public enum TradeAskOutcome
{
    Sent,
    AskerBusy,
    TargetBusy
}

public enum TradeDisconnectNotification
{
    None,

    Cancel,

    End
}

public readonly record struct TradeDisconnectResult(
    TradeDisconnectNotification Notification,
    int PartnerId,
    int SelfBigMoneyRestore = 0,
    int PartnerBigMoneyRestore = 0)
{
    public static readonly TradeDisconnectResult None = new(TradeDisconnectNotification.None, 0);
}

public sealed class TradeRegistry
{
    private readonly Dictionary<int, int> _acceptedPairs = new();

    private readonly CrossShardNegotiationTracker _crossShard = new();

    private readonly Lock _lock = new();
    private readonly Dictionary<PairKey, PairGate> _pairGates = new();
    private readonly Dictionary<int, int> _pendingByAsker = new();
    private readonly Dictionary<int, int> _pendingByTarget = new();
    private readonly Dictionary<int, TradeSession> _sessionByCharacter = new();

    public TransitionLease? TryEnterTransition(int firstCharacterId, int secondCharacterId)
    {
        if (firstCharacterId <= 0 || secondCharacterId <= 0 || firstCharacterId == secondCharacterId)
            return null;

        var pair = PairKey.Create(firstCharacterId, secondCharacterId);
        PairGate gate;

        lock (_lock)
        {
            if (!_pairGates.TryGetValue(pair, out gate!))
            {
                gate = new PairGate();
                _pairGates.Add(pair, gate);
            }

            gate.LeaseCount++;
        }

        if (!gate.Semaphore.Wait(0))
        {
            ReleaseLeaseReference(pair, gate);
            return null;
        }

        return new TransitionLease(this, pair, gate);
    }

    public bool IsBusy(int characterId)
    {
        lock (_lock)
        {
            return _pendingByAsker.ContainsKey(characterId) || _pendingByTarget.ContainsKey(characterId) ||
                   _acceptedPairs.ContainsKey(characterId) || _sessionByCharacter.ContainsKey(characterId) ||
                   _crossShard.IsPending(characterId);
        }
    }

    public bool TryPeekPending(int characterId, out int counterpartId, out bool isAsker)
    {
        lock (_lock)
        {
            if (_pendingByAsker.TryGetValue(characterId, out counterpartId))
            {
                isAsker = true;
                return true;
            }

            if (_pendingByTarget.TryGetValue(characterId, out counterpartId))
            {
                isAsker = false;
                return true;
            }

            isAsker = false;
            return false;
        }
    }

    public bool TryPeekAccepted(int characterId, out int counterpartId)
    {
        lock (_lock)
        {
            return _acceptedPairs.TryGetValue(characterId, out counterpartId);
        }
    }

    public TradeAskOutcome TryAskCrossShard(int askerId, CrossShardOutboundAsk ask)
    {
        lock (_lock)
        {
            if (IsBusy(askerId))
                return TradeAskOutcome.AskerBusy;

            return _crossShard.TryRegisterOutbound(askerId, ask) ? TradeAskOutcome.Sent : TradeAskOutcome.AskerBusy;
        }
    }

    public TradeAskOutcome TryAsk(int askerId, int targetId)
    {
        lock (_lock)
        {
            if (IsBusy(askerId))
                return TradeAskOutcome.AskerBusy;
            if (IsBusy(targetId))
                return TradeAskOutcome.TargetBusy;

            _pendingByAsker[askerId] = targetId;
            _pendingByTarget[targetId] = askerId;
            return TradeAskOutcome.Sent;
        }
    }

    public bool TryCancel(int askerId, out int targetId)
    {
        lock (_lock)
        {
            if (_pendingByAsker.TryGetValue(askerId, out targetId))
            {
                if (!_pendingByTarget.TryGetValue(targetId, out var recordedAskerId) || recordedAskerId != askerId)
                {
                    targetId = 0;
                    return false;
                }

                _pendingByAsker.Remove(askerId);
                _pendingByTarget.Remove(targetId);
                return true;
            }

            if (_crossShard.TryConsumeOutbound(askerId, out var crossShardAsk))
            {
                targetId = crossShardAsk.TargetCharacterId;
                return true;
            }

            return false;
        }
    }

    public bool TryRegisterCrossShardInbound(int targetId, CrossShardInboundAsk ask)
    {
        lock (_lock)
        {
            if (IsBusy(targetId))
                return false;

            return _crossShard.TryRegisterInbound(targetId, ask);
        }
    }

    public bool TryConsumeCrossShardInbound(int targetId, out CrossShardInboundAsk ask)
    {
        lock (_lock)
        {
            return _crossShard.TryConsumeInbound(targetId, out ask);
        }
    }

    public bool TryConsumeCrossShardOutbound(int askerId, out CrossShardOutboundAsk ask)
    {
        lock (_lock)
        {
            return _crossShard.TryConsumeOutbound(askerId, out ask);
        }
    }

    public bool TryAnswer(int targetId, bool accepted, bool askerBusyByZoneTransfer, out int askerId,
        out bool guardBlocked)
    {
        guardBlocked = false;

        lock (_lock)
        {
            if (!_pendingByTarget.TryGetValue(targetId, out askerId))
                return false;

            if (!_pendingByAsker.TryGetValue(askerId, out var recordedTargetId) || recordedTargetId != targetId)
                return false;

            if (askerBusyByZoneTransfer)
            {
                guardBlocked = true;
                return false;
            }

            _pendingByTarget.Remove(targetId);
            _pendingByAsker.Remove(askerId);

            if (accepted)
            {
                _acceptedPairs[targetId] = askerId;
                _acceptedPairs[askerId] = targetId;
            }

            return true;
        }
    }

    public bool TryStart(int callerId, int expectedOpponentId, bool opponentBusyByZoneTransfer,
        out TradeSession session)
    {
        lock (_lock)
        {
            session = null!;

            if (!_acceptedPairs.TryGetValue(callerId, out var opponentId) || opponentId != expectedOpponentId ||
                !_acceptedPairs.TryGetValue(opponentId, out var recordedCallerId) || recordedCallerId != callerId)
                return false;

            if (opponentBusyByZoneTransfer)
            {
                _acceptedPairs.Remove(callerId);
                _acceptedPairs.Remove(opponentId);
                return false;
            }

            _acceptedPairs.Remove(callerId);
            _acceptedPairs.Remove(opponentId);

            session = new TradeSession { PlayerAId = callerId, PlayerBId = opponentId };
            _sessionByCharacter[callerId] = session;
            _sessionByCharacter[opponentId] = session;
            return true;
        }
    }

    public bool TryGetSession(int characterId, out TradeSession? session)
    {
        lock (_lock)
        {
            return _sessionByCharacter.TryGetValue(characterId, out session);
        }
    }

    public bool TryEnd(int characterId, out TradeSession? session)
    {
        lock (_lock)
        {
            if (!_sessionByCharacter.TryGetValue(characterId, out session) || !session.TryClose())
                return false;

            _sessionByCharacter.Remove(characterId);
            _sessionByCharacter.Remove(session.OpponentOf(characterId));
            return true;
        }
    }

    public bool TryAbortStartForCaller(int callerId)
    {
        lock (_lock)
        {
            if (!_sessionByCharacter.TryGetValue(callerId, out var session) || !session.TryClose())
                return false;

            _sessionByCharacter.Remove(callerId);
            _sessionByCharacter.Remove(session.OpponentOf(callerId));
            return true;
        }
    }

    public bool TryBeginCommit(TradeSession session)
    {
        lock (_lock)
        {
            return _sessionByCharacter.TryGetValue(session.PlayerAId, out var fromA) &&
                   _sessionByCharacter.TryGetValue(session.PlayerBId, out var fromB) &&
                   ReferenceEquals(fromA, session) && ReferenceEquals(fromB, session) &&
                   session.TryBeginCommit();
        }
    }

    public bool TryAbortCommit(TradeSession session)
    {
        lock (_lock)
        {
            if (!_sessionByCharacter.TryGetValue(session.PlayerAId, out var fromA) ||
                !_sessionByCharacter.TryGetValue(session.PlayerBId, out var fromB) ||
                !ReferenceEquals(fromA, session) || !ReferenceEquals(fromB, session) ||
                !session.IsCommitInProgress)
                return false;

            session.CompleteCommit();
            _sessionByCharacter.Remove(session.PlayerAId);
            _sessionByCharacter.Remove(session.PlayerBId);
            return true;
        }
    }

    public bool TryCompleteCommit(TradeSession session)
    {
        return TryAbortCommit(session);
    }

    public TradeDisconnectResult ClearForDisconnect(int characterId)
    {
        lock (_lock)
        {
            if (_pendingByAsker.Remove(characterId, out var target))
            {
                _pendingByTarget.Remove(target);
                return new TradeDisconnectResult(TradeDisconnectNotification.Cancel, target);
            }

            if (_pendingByTarget.Remove(characterId, out var asker))
            {
                _pendingByAsker.Remove(asker);
                return new TradeDisconnectResult(TradeDisconnectNotification.Cancel, asker);
            }

            if (_acceptedPairs.Remove(characterId, out var acceptedPartner))
            {
                _acceptedPairs.Remove(acceptedPartner);
                return new TradeDisconnectResult(TradeDisconnectNotification.Cancel, acceptedPartner);
            }

            if (_sessionByCharacter.TryGetValue(characterId, out var session))
            {
                if (session.IsCommitInProgress || !session.TryClose())
                    return TradeDisconnectResult.None;

                _sessionByCharacter.Remove(characterId);
                var opponentId = session.OpponentOf(characterId);
                _sessionByCharacter.Remove(opponentId);
                return new TradeDisconnectResult(TradeDisconnectNotification.End, opponentId,
                    session.SideOf(characterId).BigMoney, session.SideOf(opponentId).BigMoney);
            }

            _crossShard.TryConsumeOutbound(characterId, out _);
            return TradeDisconnectResult.None;
        }
    }

    public void ClearForWorldEntry(int characterId)
    {
        lock (_lock)
        {
            if (_pendingByAsker.Remove(characterId, out var pendingTarget))
                RemoveMirror(_pendingByTarget, pendingTarget, characterId);
            if (_pendingByTarget.Remove(characterId, out var pendingAsker))
                RemoveMirror(_pendingByAsker, pendingAsker, characterId);

            if (_acceptedPairs.Remove(characterId, out var acceptedPartner))
                RemoveMirror(_acceptedPairs, acceptedPartner, characterId);

            if (_sessionByCharacter.TryGetValue(characterId, out var session) && session.TryClose())
            {
                _sessionByCharacter.Remove(characterId);
                _sessionByCharacter.Remove(session.OpponentOf(characterId));
            }

            _crossShard.ClearForCharacter(characterId);
        }
    }

    private static void RemoveMirror(Dictionary<int, int> map, int counterpartId, int expectedValue)
    {
        if (map.TryGetValue(counterpartId, out var mirror) && mirror == expectedValue)
            map.Remove(counterpartId);
    }

    private void ReleaseTransition(PairKey pair, PairGate gate)
    {
        gate.Semaphore.Release();
        ReleaseLeaseReference(pair, gate);
    }

    private void ReleaseLeaseReference(PairKey pair, PairGate gate)
    {
        lock (_lock)
        {
            gate.LeaseCount--;
            if (gate.LeaseCount == 0 && !IsTracked(pair))
                _pairGates.Remove(pair);
        }
    }

    private bool IsTracked(PairKey pair)
    {
        return HasPair(_pendingByAsker, pair) || HasPair(_pendingByTarget, pair) ||
               HasPair(_acceptedPairs, pair) || HasPair(_sessionByCharacter, pair);
    }

    private static bool HasPair<T>(Dictionary<int, T> map, PairKey pair)
        where T : class
    {
        return map.TryGetValue(pair.LowerCharacterId, out _) &&
               map.ContainsKey(pair.HigherCharacterId);
    }

    private static bool HasPair(Dictionary<int, int> map, PairKey pair)
    {
        return map.TryGetValue(pair.LowerCharacterId, out var lowerCounterpart) &&
               lowerCounterpart == pair.HigherCharacterId &&
               map.TryGetValue(pair.HigherCharacterId, out var higherCounterpart) &&
               higherCounterpart == pair.LowerCharacterId;
    }

    internal readonly record struct PairKey(int LowerCharacterId, int HigherCharacterId)
    {
        internal static PairKey Create(int firstCharacterId, int secondCharacterId)
        {
            return firstCharacterId < secondCharacterId
                ? new PairKey(firstCharacterId, secondCharacterId)
                : new PairKey(secondCharacterId, firstCharacterId);
        }
    }

    internal sealed class PairGate
    {
        public int LeaseCount;

        public SemaphoreSlim Semaphore { get; } = new(1, 1);
    }

    public sealed class TransitionLease : IDisposable
    {
        private readonly PairGate _gate;
        private readonly PairKey _pair;
        private TradeRegistry? _owner;

        internal TransitionLease(TradeRegistry owner, PairKey pair, PairGate gate)
        {
            _owner = owner;
            _pair = pair;
            _gate = gate;
        }

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            if (owner is not null)
                owner.ReleaseTransition(_pair, _gate);
        }
    }
}
