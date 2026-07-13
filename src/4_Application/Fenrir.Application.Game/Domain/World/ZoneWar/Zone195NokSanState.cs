using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class Zone195NokSanState
{
    public const int StoneSlotCount = 9;

    public const int MaxStonesPerTribe = 4;

    public const int MonsterDamageBonusPerStone = 100;

    public const float DefaultCaptureRadius = 12.5f;

    public const float DefaultPostX = -20.0f;

    public const float DefaultPostZ = 2510.0f;

    public static readonly int TribeCount = WorldStateService.TribeCount;

    private readonly Lock _lock = new();

    private readonly int[] _owners = new int[StoneSlotCount];

    private readonly int[] _stonesHeld = new int[TribeCount];

    public static bool IsValidSlot(int slotIndex)
    {
        return slotIndex is >= 0 && slotIndex < StoneSlotCount;
    }

    public static bool IsValidTribe(int tribeId)
    {
        return tribeId is >= 0 && tribeId < TribeCount;
    }

    public int GetOwner(int slotIndex)
    {
        ValidateSlot(slotIndex);
        lock (_lock)
        {
            return _owners[slotIndex];
        }
    }

    public byte? GetOwningTribe(int slotIndex)
    {
        var raw = GetOwner(slotIndex);
        return raw == 0 ? null : (byte)(raw - 1);
    }

    public int GetStonesHeld(byte tribeId)
    {
        ValidateTribe(tribeId);
        lock (_lock)
        {
            return _stonesHeld[tribeId];
        }
    }

    public int GetMonsterDamageBonus(byte tribeId)
    {
        if (!IsValidTribe(tribeId))
            return 0;

        return GetStonesHeld(tribeId) * MonsterDamageBonusPerStone;
    }

    public void CommitCapture(int slotIndex, byte capturingTribe)
    {
        ValidateSlot(slotIndex);
        ValidateTribe(capturingTribe);

        lock (_lock)
        {
            var priorOwner = _owners[slotIndex];
            if (priorOwner != 0)
            {
                var priorTribe = priorOwner - 1;
                if (priorTribe >= 0 && priorTribe < TribeCount)
                    _stonesHeld[priorTribe] = Math.Max(0, _stonesHeld[priorTribe] - 1);
            }

            _stonesHeld[capturingTribe] = Math.Min(MaxStonesPerTribe, _stonesHeld[capturingTribe] + 1);
            _owners[slotIndex] = capturingTribe + 1;
        }
    }

    public Zone195NokSanStateSnapshot Snapshot()
    {
        lock (_lock)
        {
            return new Zone195NokSanStateSnapshot(
                [.. _stonesHeld],
                [.. _owners]);
        }
    }

    private static void ValidateSlot(int slotIndex)
    {
        if (!IsValidSlot(slotIndex))
            throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex,
                $"Zone195 stone slot must be 0-{StoneSlotCount - 1}.");
    }

    private static void ValidateTribe(byte tribeId)
    {
        if (!IsValidTribe(tribeId))
            throw new ArgumentOutOfRangeException(nameof(tribeId), tribeId, $"TribeId must be 0-{TribeCount - 1}.");
    }
}

public readonly record struct Zone195NokSanStateSnapshot(
    ImmutableArray<int> StonesHeld,
    ImmutableArray<int> Owners);
