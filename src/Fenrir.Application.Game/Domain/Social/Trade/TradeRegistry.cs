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
    private readonly Dictionary<int, int> _pendingByAsker = new();
    private readonly Dictionary<int, int> _pendingByTarget = new();
    private readonly Dictionary<int, TradeSession> _sessionByCharacter = new();

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
            if (_pendingByAsker.Remove(askerId, out targetId))
                return true;

            if (_crossShard.TryConsumeOutbound(askerId, out var crossShardAsk))
            {
                targetId = crossShardAsk.TargetCharacterId;
                return true;
            }

            return false;
        }
    }

    public bool ClearTargetAfterCancel(int targetId, int askerId)
    {
        lock (_lock)
        {
            return _pendingByTarget.TryGetValue(targetId, out var recordedAskerId) &&
                   recordedAskerId == askerId &&
                   _pendingByTarget.Remove(targetId);
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
            if (!_pendingByTarget.Remove(targetId, out askerId))
                return false;

            if (!_pendingByAsker.TryGetValue(askerId, out var recordedTargetId) || recordedTargetId != targetId)
                return false;

            if (accepted)
                _acceptedPairs[targetId] = askerId;

            if (askerBusyByZoneTransfer)
            {
                guardBlocked = true;
                return false;
            }

            _pendingByAsker.Remove(askerId);

            if (accepted)
                _acceptedPairs[askerId] = targetId;

            return true;
        }
    }

    public bool TryStart(int callerId, int expectedOpponentId, bool opponentBusyByZoneTransfer,
        out TradeSession session)
    {
        lock (_lock)
        {
            session = null!;

            if (!_acceptedPairs.TryGetValue(callerId, out var opponentId) || opponentId != expectedOpponentId)
                return false;

            if (opponentBusyByZoneTransfer)
            {
                _acceptedPairs.Remove(callerId);
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
            if (!_sessionByCharacter.Remove(characterId, out session))
                return false;

            _sessionByCharacter.Remove(session.OpponentOf(characterId));
            return true;
        }
    }

    public bool TryAbortStartForCaller(int callerId)
    {
        lock (_lock)
        {
            return _sessionByCharacter.Remove(callerId);
        }
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

            if (_sessionByCharacter.Remove(characterId, out var session))
            {
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

            if (_sessionByCharacter.Remove(characterId, out var session))
                _sessionByCharacter.Remove(session.OpponentOf(characterId));

            _crossShard.ClearForCharacter(characterId);
        }
    }

    private static void RemoveMirror(Dictionary<int, int> map, int counterpartId, int expectedValue)
    {
        if (map.TryGetValue(counterpartId, out var mirror) && mirror == expectedValue)
            map.Remove(counterpartId);
    }
}
