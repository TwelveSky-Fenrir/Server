namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Process-wide, thread-safe storage for the Zone051 (6 slots) and Zone053 (10 slots) siege-state-VALUE
///     arrays -- the two numbered families the A5-zone051-053-states behavior contract covers, structurally
///     identical in shape to <see cref="ZoneCenterSiegeState" />'s own Zone049/Zone175/Zone267/Zone241/Zone335
///     fields but kept in a separate class rather than added to that one, since <c>ZoneCenterSiegeState.cs</c>
///     is a shared file several concurrent wave-13 "A5-*" slices independently target -- see this class's own
///     wiring-manifest notes (reported by the originating slice, not applied here) for how a single later
///     integration pass folds a reference to this type into <see cref="ZoneCenterBroadcastIngestor" />'s own
///     constructor and dispatch switch.
///     <para>
///         Deliberately holds ONLY the state-VALUE arrays, no paired state-TIME array. The A5 contract's own
///         Side effect 2 confirms neither family's state-time array is ever written by any packet-driven
///         selector this class's resolver applies (a real asymmetry against Zone049, whose own switch DOES
///         stamp its paired array on 4 of its 9 sub-codes, see <see cref="ZoneCenterSiegeState.SetZone049State" />).
///         The state-time arrays exist in legacy in-memory storage but are only ever touched by the
///         lifecycle-driven startup (stamp-to-now, immediately after the DB load) and periodic-persistence
///         (every sixth server logic tick) triggers the contract's own Side effects 4-5 describe -- both are
///         explicitly "not packet-triggered" and out of scope for this packet-driven relay slice; a future
///         workstream that wants byte-exact startup/persistence parity for these two families needs its own
///         dedicated boot-preload/write-behind design, not an addition here.
///     </para>
///     <para>
///         Deliberately NOT persisted, matching <see cref="ZoneCenterSiegeState" />'s own documented precedent
///         for the identical underlying reason: legacy ts25center never wrote either family to SQL on its own
///         initiative -- persistence is always deferred to the periodic flush of the WHOLE WORLD_INFO
///         structure (A5 contract Side effect 5), never a byte this class's own resolver triggers directly. A
///         future feature that needs these to survive a process restart needs its own schema + repository from
///         a database-engineer workstream first.
///     </para>
/// </summary>
/// <remarks>
///     Réf. C++ : Server/Header/Protocol/STRUCT.h:577-579,607-612 (the true slot counts -- 13/6/10 -- for the
///     Zone049/Zone051/Zone053 families' storage-array declarations, back-to-back, verified by direct read per
///     the A5 contract's own Citations section). Contract:
///     scratchpad/contracts/wave13/A5-zone051-053-states.md.
/// </remarks>
public sealed class Zone051Zone053SiegeState
{
    /// <summary>6 tracked Zone051 slots (STRUCT.h:577-579,607-612).</summary>
    public const int Zone051Slots = 6;

    /// <summary>10 tracked Zone053 slots (STRUCT.h:577-579,607-612).</summary>
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

    /// <summary>
    ///     Unconditional overwrite of <paramref name="slot" />'s state -- no companion state-time write (see
    ///     class remarks). <paramref name="state" /> is expected to already be the mapped legacy state value
    ///     from <see cref="Zone051Zone053StateMap.TryMapZone051" />, never the raw selector.
    /// </summary>
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

    /// <summary>
    ///     Unconditional overwrite of <paramref name="slot" />'s state -- no companion state-time write (see
    ///     class remarks). <paramref name="state" /> is expected to already be the mapped legacy state value
    ///     from <see cref="Zone051Zone053StateMap.TryMapZone053" />, never the raw selector.
    /// </summary>
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
