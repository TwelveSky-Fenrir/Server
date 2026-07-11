using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class TribeSymbolCombatModifiers
{

        public const float OwnSymbolLostDamageDownPenalty = 0.2f;

        public const int MaxDamageUpBonusIncrementCount = 4;

        public const int SmallTribeAdvantagePointFloor = 10;

    private readonly int[] _damageUpBonusIncrementCount = new int[WorldStateService.TribeCount];
    private readonly float[] _damageDownPenalty = new float[WorldStateService.TribeCount];
    private readonly Lock _lock = new();

        public float GetDamageDownPenalty(byte tribeId)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            return _damageDownPenalty[tribeId];
        }
    }

    internal void SetDamageDownPenalty(byte tribeId, float value)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            _damageDownPenalty[tribeId] = value;
        }
    }

        public int GetDamageUpBonusIncrementCount(byte tribeId)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            return _damageUpBonusIncrementCount[tribeId];
        }
    }

    internal void SetDamageUpBonusIncrementCount(byte tribeId, int value)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            _damageUpBonusIncrementCount[tribeId] = value;
        }
    }

    private static void ValidateTribeId(byte tribeId)
    {
        if (tribeId >= WorldStateService.TribeCount)
            throw new ArgumentOutOfRangeException(nameof(tribeId), tribeId,
                $"TribeId must be 0-{WorldStateService.TribeCount - 1}.");
    }
}
