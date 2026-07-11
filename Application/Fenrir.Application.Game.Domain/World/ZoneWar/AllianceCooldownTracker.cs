using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class AllianceCooldownTracker
{
    private readonly DateOnly?[] _cooldownUntil = new DateOnly?[WorldStateService.TribeCount];
    private readonly Lock _lock = new();

        public DateOnly? GetCooldownUntil(byte tribeId)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            return _cooldownUntil[tribeId];
        }
    }

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
