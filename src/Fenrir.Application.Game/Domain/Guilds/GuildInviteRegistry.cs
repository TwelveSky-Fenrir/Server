using Fenrir.Application.Game.Domain.Social;

namespace Fenrir.Application.Game.Domain.Guilds;

public enum GuildInviteAskOutcome
{
    Sent,
    AskerBusy,
    TargetBusy
}

public sealed class GuildInviteRegistry
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

    public GuildInviteAskOutcome TryAskCrossShard(int askerId, CrossShardOutboundAsk ask)
    {
        lock (_lock)
        {
            if (IsNegotiating(askerId))
                return GuildInviteAskOutcome.AskerBusy;

            return _crossShard.TryRegisterOutbound(askerId, ask)
                ? GuildInviteAskOutcome.Sent
                : GuildInviteAskOutcome.AskerBusy;
        }
    }

    public GuildInviteAskOutcome TryAsk(int askerId, int targetId)
    {
        lock (_lock)
        {
            if (IsNegotiating(askerId))
                return GuildInviteAskOutcome.AskerBusy;
            if (IsNegotiating(targetId))
                return GuildInviteAskOutcome.TargetBusy;

            _pendingByAsker[askerId] = targetId;
            _pendingByTarget[targetId] = askerId;
            return GuildInviteAskOutcome.Sent;
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

    public bool TryWithdrawAsk(int askerId, out int targetId)
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

    public bool TryAcknowledgeWithdrawal(int targetId, int expectedAskerId)
    {
        lock (_lock)
        {
            if (_pendingByTarget.TryGetValue(targetId, out var recordedAskerId) && recordedAskerId == expectedAskerId)
            {
                _pendingByTarget.Remove(targetId);
                return true;
            }

            return false;
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

            if (askerBusyByZoneTransfer)
            {
                guardBlocked = true;
                return false;
            }

            _pendingByAsker.Remove(askerId);

            if (accepted)
                _acceptedFor[askerId] = targetId;

            return true;
        }
    }

    public bool TryConsumeAccepted(int askerId, out int targetId)
    {
        lock (_lock)
        {
            return _acceptedFor.Remove(askerId, out targetId);
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

    public bool TryConsumeCrossShardOutbound(int askerId, byte targetShardId, int targetCharacterId,
        long correlationToken, out CrossShardOutboundAsk ask)
    {
        lock (_lock)
        {
            return _crossShard.TryConsumeOutbound(askerId, targetShardId, targetCharacterId, correlationToken,
                out ask);
        }
    }

    public void MarkAccepted(int askerId, int targetId)
    {
        lock (_lock)
        {
            _acceptedFor[askerId] = targetId;
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

            _acceptedFor.Remove(characterId);

            _crossShard.ClearForCharacter(characterId);
        }
    }

    private static void RemoveMirror(Dictionary<int, int> map, int counterpartId, int expectedValue)
    {
        if (map.TryGetValue(counterpartId, out var mirror) && mirror == expectedValue)
            map.Remove(counterpartId);
    }
}
