namespace Fenrir.Application.Game.Domain.Social;

public readonly record struct CrossShardOutboundAsk(
    byte TargetShardId,
    int TargetCharacterId,
    string TargetAvatarName,
    int SourceCombinedLevel = 0,
    long CorrelationToken = 0);

public readonly record struct CrossShardInboundAsk(
    long RelayId,
    byte SourceShardId,
    int SourceCharacterId,
    string SourceAvatarName);

public sealed class CrossShardNegotiationTracker
{
    private readonly Dictionary<int, CrossShardInboundAsk> _inboundByTarget = new();
    private readonly Lock _lock = new();
    private readonly Dictionary<int, CrossShardOutboundAsk> _outboundByAsker = new();

    public bool IsPending(int characterId)
    {
        lock (_lock)
        {
            return _outboundByAsker.ContainsKey(characterId) || _inboundByTarget.ContainsKey(characterId);
        }
    }

    public bool TryRegisterOutbound(int askerId, CrossShardOutboundAsk ask)
    {
        lock (_lock)
        {
            return _outboundByAsker.TryAdd(askerId, ask);
        }
    }

    public bool TryConsumeOutbound(int askerId, byte targetShardId, int targetCharacterId,
        long correlationToken,
        out CrossShardOutboundAsk ask)
    {
        lock (_lock)
        {
            if (!_outboundByAsker.TryGetValue(askerId, out ask) ||
                ask.TargetShardId != targetShardId || ask.TargetCharacterId != targetCharacterId ||
                ask.CorrelationToken != correlationToken)
            {
                ask = default;
                return false;
            }

            return _outboundByAsker.Remove(askerId);
        }
    }

    public bool TryConsumeOutbound(int askerId, byte targetShardId, int targetCharacterId,
        out CrossShardOutboundAsk ask)
    {
        lock (_lock)
        {
            if (!_outboundByAsker.TryGetValue(askerId, out ask) ||
                ask.TargetShardId != targetShardId || ask.TargetCharacterId != targetCharacterId)
            {
                ask = default;
                return false;
            }

            return _outboundByAsker.Remove(askerId);
        }
    }

    public bool TryConsumeOutbound(int askerId, out CrossShardOutboundAsk ask)
    {
        lock (_lock)
        {
            return _outboundByAsker.Remove(askerId, out ask);
        }
    }

    public bool TryRegisterInbound(int targetId, CrossShardInboundAsk ask)
    {
        lock (_lock)
        {
            return _inboundByTarget.TryAdd(targetId, ask);
        }
    }

    public bool TryConsumeInbound(int targetId, out CrossShardInboundAsk ask)
    {
        lock (_lock)
        {
            return _inboundByTarget.Remove(targetId, out ask);
        }
    }

    public bool TryClearInbound(int targetId, byte sourceShardId, int sourceCharacterId)
    {
        lock (_lock)
        {
            if (!_inboundByTarget.TryGetValue(targetId, out var ask) ||
                ask.SourceShardId != sourceShardId || ask.SourceCharacterId != sourceCharacterId)
                return false;

            return _inboundByTarget.Remove(targetId);
        }
    }

    public void ClearForCharacter(int characterId)
    {
        lock (_lock)
        {
            _outboundByAsker.Remove(characterId);
            _inboundByTarget.Remove(characterId);
        }
    }
}
