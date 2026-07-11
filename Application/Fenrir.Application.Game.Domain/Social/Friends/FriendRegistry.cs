namespace Fenrir.Application.Game.Domain.Social.Friends;

public enum FriendAskOutcome
{
    Sent,
    AskerBusy,
    TargetBusy
}

public sealed class FriendRegistry
{
    private readonly Dictionary<int, int> _acceptedFor = new();

    private readonly CrossShardNegotiationTracker _crossShard = new();

    private readonly Lock _lock = new();
    private readonly Dictionary<int, int> _pendingByAsker = new();
    private readonly Dictionary<int, int> _pendingByTarget = new();

    public bool IsNegotiating(int characterId)
    {
        lock (_lock)
        {
            return _pendingByAsker.ContainsKey(characterId) || _pendingByTarget.ContainsKey(characterId) ||
                   _acceptedFor.ContainsKey(characterId) || _acceptedFor.ContainsValue(characterId) ||
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

    public FriendAskOutcome TryAsk(int askerId, int targetId)
    {
        lock (_lock)
        {
            if (IsNegotiating(askerId))
                return FriendAskOutcome.AskerBusy;
            if (IsNegotiating(targetId))
                return FriendAskOutcome.TargetBusy;

            _pendingByAsker[askerId] = targetId;
            _pendingByTarget[targetId] = askerId;
            return FriendAskOutcome.Sent;
        }
    }

    public FriendAskOutcome TryAskCrossShard(int askerId, CrossShardOutboundAsk ask)
    {
        lock (_lock)
        {
            if (IsNegotiating(askerId))
                return FriendAskOutcome.AskerBusy;

            return _crossShard.TryRegisterOutbound(askerId, ask) ? FriendAskOutcome.Sent : FriendAskOutcome.AskerBusy;
        }
    }

    public bool TryCancel(int askerId, out int targetId)
    {
        lock (_lock)
        {
            if (_pendingByAsker.Remove(askerId, out targetId))
            {
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
            if (IsNegotiating(targetId))
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

    public void MarkAccepted(int characterId, int otherCharacterId)
    {
        lock (_lock)
        {
            _acceptedFor[characterId] = otherCharacterId;
        }
    }

    public bool TryAnswer(int targetId, bool accepted, out int askerId)
    {
        lock (_lock)
        {
            if (!_pendingByTarget.Remove(targetId, out askerId))
                return false;

            _pendingByAsker.Remove(askerId);

            if (accepted)
            {
                _acceptedFor[askerId] = targetId;
                _acceptedFor[targetId] = askerId;
            }

            return true;
        }
    }

    public bool TryConsumeAccepted(int characterId, out int otherId)
    {
        lock (_lock)
        {
            return _acceptedFor.Remove(characterId, out otherId);
        }
    }
}
