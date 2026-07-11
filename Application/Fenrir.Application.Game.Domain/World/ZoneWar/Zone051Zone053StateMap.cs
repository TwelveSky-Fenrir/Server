namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     The byte-exact legacy selector-code &#8594; state-value tables for the ts25center
///     <c>ZONE_BROADCAST_FOR_CENTER_SEND</c> (wire opcode 33) handler's Zone051 (selectors 10-18, 6 slots) and
///     Zone053 (selectors 19-30, 10 slots) families -- the two numbered siege-state machines adjacent to, but
///     structurally identical in shape to, the already-bound Zone049/Zone175/Zone267/Zone241/Zone335 families
///     in <see cref="SiegeEventStateMap" />.
///     <para>
///         Kept as its own file rather than folded into <see cref="SiegeEventStateMap" /> for two reasons: (1)
///         several concurrent wave-13 "A5-*" slices independently target that shared file plus
///         <see cref="ZoneCenterSiegeState" />/<see cref="ZoneCenterBroadcastIngestor" /> -- this slice
///         deliberately avoids editing any of the three and instead reports its own wiring in a manifest for a
///         single coordinated later merge; (2) per the A5-zone051-053-states behavior contract's own "Central
///         finding," no first-party <c>ts25zone</c> process ever transmits a selector in either range in any
///         shipped build (every sender-side code path is compiled out under a dead feature guard) -- this table
///         exists purely so the RECEIVING <c>ts25center</c>-side handler, which IS live and reachable, behaves
///         byte-exactly if a selector in either range is ever relayed to it, not because either family is
///         actually exercised in production today.
///     </para>
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25center/S04_MyWork02.cpp:254-289 (Zone051, selectors 10-18, full case bodies) ;
///     :290-335 (Zone053, selectors 19-30, full case bodies, including the dead write at :296-297). Contract:
///     scratchpad/contracts/wave13/A5-zone051-053-states.md (fully cited; every value below is taken verbatim
///     from that contract's own mapping table, not re-derived from Server/ this session).
/// </remarks>
public static class Zone051Zone053StateMap
{
    /// <summary>
    ///     Zone051 selectors 10-18 &#8594; states {no-op, 1, 2, 3, 5, 5, 4, 5, 0}. Selector 10 is the family's
    ///     block-opening case (no state write) and returns <see langword="false" />; selector 18 is a real
    ///     reset-to-0 write and returns <see langword="true" /> with <paramref name="state" /> = 0. Returns
    ///     <see langword="false" /> for any selector outside 10-18 or matching no documented case within it.
    ///     (Server/ts25center/S04_MyWork02.cpp:257-289)
    /// </summary>
    public static bool TryMapZone051(int selector, out int state)
    {
        state = selector switch
        {
            11 => 1,
            12 => 2,
            13 => 3,
            14 => 5,
            15 => 5,
            16 => 4,
            17 => 5,
            18 => 0,
            _ => -1
        };

        return state >= 0;
    }

    /// <summary>
    ///     Zone053 selectors 19-30 &#8594; states {dead-write, 1, 2, 3, 5, 5, no-op, no-op, no-op, 4, 5, 0}.
    ///     Selector 19's intended write (100) never executes in any shipped build -- the guarding
    ///     <c>__GOD__</c> identifier is unconditionally defined in every build-variant branch with no
    ///     <c>#undef</c> anywhere in the traced codebase, so the local write inside that case is compiled out.
    ///     This method therefore returns <see langword="false" /> for selector 19, matching OBSERVED live
    ///     behavior (no write), never the source's dead intent (write 100) -- see the A5 contract's own Edge
    ///     cases for the full guard-chain citation. Selectors 25-27 are bare no-op cases (25/27 were, per the
    ///     contract's archaeology, intended to carry an extra payload this handler never reads). Selector 30
    ///     is a real reset-to-0 write. Returns <see langword="false" /> for any selector outside 19-30 or
    ///     matching no documented case within it.
    ///     (Server/ts25center/S04_MyWork02.cpp:293-335, including the dead write at :296-297)
    /// </summary>
    public static bool TryMapZone053(int selector, out int state)
    {
        state = selector switch
        {
            20 => 1,
            21 => 2,
            22 => 3,
            23 => 5,
            24 => 5,
            28 => 4,
            29 => 5,
            30 => 0,
            _ => -1
        };

        return state >= 0;
    }
}
