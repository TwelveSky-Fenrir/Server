namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class Zone051Zone053SiegeState
{
    public const int Zone051Slots = 6;

    public const int Zone053Slots = 10;

    private readonly Lock _lock = new();
    private readonly int[] _zone051State = new int[Zone051Slots];
    private readonly int[] _zone053State = new int[Zone053Slots];

    public static bool IsValidZone051Slot(int slot)
    {
        return slot is >= 0 and < Zone051Slots;
    }

    public static bool IsValidZone053Slot(int slot)
    {
        return slot is >= 0 and < Zone053Slots;
    }

    public int GetZone051(int slot)
    {
        ValidateZone051Slot(slot);
        lock (_lock)
        {
            return _zone051State[slot];
        }
    }

    public void SetZone051(int slot, int state)
    {
        ValidateZone051Slot(slot);
        lock (_lock)
        {
            _zone051State[slot] = state;
        }
    }

    public int GetZone053(int slot)
    {
        ValidateZone053Slot(slot);
        lock (_lock)
        {
            return _zone053State[slot];
        }
    }

    public void SetZone053(int slot, int state)
    {
        ValidateZone053Slot(slot);
        lock (_lock)
        {
            _zone053State[slot] = state;
        }
    }

    private static void ValidateZone051Slot(int slot)
    {
        if (!IsValidZone051Slot(slot))
            throw new ArgumentOutOfRangeException(nameof(slot), slot,
                $"Zone051 slot must be 0-{Zone051Slots - 1}.");
    }

    private static void ValidateZone053Slot(int slot)
    {
        if (!IsValidZone053Slot(slot))
            throw new ArgumentOutOfRangeException(nameof(slot), slot,
                $"Zone053 slot must be 0-{Zone053Slots - 1}.");
    }
}
