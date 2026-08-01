using System.Collections.Concurrent;

namespace Fenrir.Application.Game.Domain.Combat;

public sealed class KillCooldownTracker
{
    public const int MissionKillOtherTribeCap = 10;

    public static readonly TimeSpan DefaultCooldown = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<(int AttackerId, int DefenderId), DateTime> _lastRewardedKillUtc = new();

    public bool TryRegisterKill(int attackerId, int defenderId, DateTime utcNow, TimeSpan? cooldown = null)
    {
        var window = cooldown ?? DefaultCooldown;
        var key = (attackerId, defenderId);

        while (true)
        {
            if (!_lastRewardedKillUtc.TryGetValue(key, out var lastGrantedUtc))
            {
                if (_lastRewardedKillUtc.TryAdd(key, utcNow))
                    return true;

                continue;
            }

            if (utcNow - lastGrantedUtc < window)
                return false;

            if (_lastRewardedKillUtc.TryUpdate(key, utcNow, lastGrantedUtc))
                return true;
        }
    }
}
