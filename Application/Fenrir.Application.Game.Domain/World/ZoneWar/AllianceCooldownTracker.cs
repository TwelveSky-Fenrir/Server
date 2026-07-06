using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     The per-tribe "earliest date a new alliance is allowed" cooldown that
///     <see cref="AllianceDiplomacyCeremony" />'s already-allied-negotiation completion (and the separate
///     alliance-stone-destruction mechanism it is NOT part of) writes.
/// </summary>
/// <remarks>
///     GAP: <c>game.WorldStateTribes</c> has no column for this cooldown date yet (unlike
///     <see cref="WorldStateService" />'s Points/HasSymbol/IsClosed fields, which are fully durable) -- this
///     class is in-process memory only and resets on every Fenrir GameServer restart. A durable version needs
///     a new column (e.g. <c>AllianceCooldownUntilUtc</c>) plus <c>IWorldStateRepository</c>/stored-procedure
///     support, which is a fenrir-database-engineer follow-up, out of scope for the Domain/Hosting-only change
///     that introduced this tracker -- flagged here rather than silently left non-durable.
/// </remarks>
public sealed class AllianceCooldownTracker
{
    private readonly DateOnly?[] _cooldownUntil = new DateOnly?[WorldStateService.TribeCount];
    private readonly Lock _lock = new();

    /// <summary>Null if <paramref name="tribeId" /> is not currently under a re-alliance cooldown.</summary>
    public DateOnly? GetCooldownUntil(byte tribeId)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            return _cooldownUntil[tribeId];
        }
    }

    /// <summary>
    ///     True if <paramref name="today" /> has not yet reached <paramref name="tribeId" />'s recorded eligible-again
    ///     date.
    /// </summary>
    public bool IsInCooldown(byte tribeId, DateOnly today)
    {
        return GetCooldownUntil(tribeId) is { } until && today < until;
    }

    public void SetCooldownUntil(byte tribeId, DateOnly until)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            _cooldownUntil[tribeId] = until;
        }
    }

    private static void ValidateTribeId(byte tribeId)
    {
        if (tribeId >= WorldStateService.TribeCount)
            throw new ArgumentOutOfRangeException(nameof(tribeId), tribeId,
                $"TribeId must be 0-{WorldStateService.TribeCount - 1}.");
    }
}
