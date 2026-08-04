namespace Fenrir.Application.Game.Domain.World;

public sealed class CharacterPresenceOwnership
{
    private const int GateCount = 1024;

    private readonly SemaphoreSlim[] _gates = Enumerable.Range(0, GateCount)
        .Select(static _ => new SemaphoreSlim(1, 1))
        .ToArray();

    private readonly Dictionary<int, long> _ownerSessionIds = [];

    private readonly object _ownersLock = new();

    public async ValueTask PublishAsync(int characterId, long ownerSessionId,
        Func<CancellationToken, ValueTask> publish, CancellationToken cancellationToken)
    {
        var gate = GetGate(characterId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        long? previousOwnerSessionId;
        lock (_ownersLock)
        {
            previousOwnerSessionId = _ownerSessionIds.TryGetValue(characterId, out var previousOwner)
                ? previousOwner
                : null;
            _ownerSessionIds[characterId] = ownerSessionId;
        }

        try
        {
            await publish(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            lock (_ownersLock)
            {
                if (IsOwner(characterId, ownerSessionId))
                    if (previousOwnerSessionId is { } previous)
                        _ownerSessionIds[characterId] = previous;
                    else
                        _ownerSessionIds.Remove(characterId);
            }

            throw;
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<bool> RemoveIfOwnerAsync(int characterId, long ownerSessionId,
        Func<CancellationToken, ValueTask> remove, CancellationToken cancellationToken)
    {
        var gate = GetGate(characterId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            lock (_ownersLock)
            {
                if (!IsOwner(characterId, ownerSessionId))
                    return false;
            }

            await remove(cancellationToken).ConfigureAwait(false);

            lock (_ownersLock)
            {
                if (IsOwner(characterId, ownerSessionId))
                    _ownerSessionIds.Remove(characterId);
            }

            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    private SemaphoreSlim GetGate(int characterId)
    {
        return _gates[(int)((uint)characterId % GateCount)];
    }

    private bool IsOwner(int characterId, long ownerSessionId)
    {
        return _ownerSessionIds.TryGetValue(characterId, out var currentOwnerSessionId) &&
               currentOwnerSessionId == ownerSessionId;
    }
}
