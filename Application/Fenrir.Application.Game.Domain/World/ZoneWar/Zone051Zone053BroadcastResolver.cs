using System.Buffers.Binary;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     The relay-passthrough resolver for the Zone051/Zone053 families -- mirrors the shape of
///     <see cref="ZoneCenterBroadcastIngestor" />'s own private <c>ApplyZone175</c>/<c>ApplyZone267</c>/
///     <c>ApplyZone241</c> methods (read the zone-slot index from the first 4 payload bytes, bounds-check it,
///     resolve the mapped state via <see cref="Zone051Zone053StateMap" />, write it if mapped) so a single
///     later integration pass can wire this in with two one-line <c>case</c> arms inside that class's own
///     <c>ApplyStateEffect</c> switch, rather than duplicating this logic inline in a shared file this slice
///     deliberately does not edit. See this cluster's own wiring manifest for the exact before/after snippet.
///     <para>
///         Deliberately does NOT perform the "unconditional relay" (the A5 contract's own Side effect 3)
///         itself -- <see cref="ZoneCenterBroadcastIngestor.Ingest" /> already relays every accepted event
///         code unconditionally, AFTER calling into whatever <c>ApplyStateEffect</c> resolves to, including a
///         resolver call here that performs no write at all (selectors 10/19/25/26/27, or any selector outside
///         10-30 that never reaches this resolver in the first place). Wiring the two dispatch cases below is
///         therefore the ONLY change the relay side needs; this class itself needs no relay knowledge.
///     </para>
///     <para>
///         Bounds-checks the zone-slot index even though the legacy center does not (see the A5 contract's own
///         Inputs section, flagged there only as an unclassified hardening concern, never resolved to a
///         specific severity) -- the same defensive posture <see cref="ZoneCenterBroadcastIngestor" />'s own
///         <c>ApplyZone175</c>/<c>ApplyZone267</c>/<c>ApplyZone241</c> already apply for their own families.
///         An out-of-range slot is logged and the write is skipped; the caller's own unconditional relay still
///         proceeds regardless (this resolver has no way to prevent that, by design -- see above).
///     </para>
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25center/S04_MyWork02.cpp:212-216 (dispatch opening on the selector value) ;
///     :254-289 (Zone051 case bodies) ; :290-335 (Zone053 case bodies) ; Server/Header/Protocol/DEFINE.h:413
///     (the 4-byte extraction width for the zone-slot index, same width every sibling family in
///     <see cref="ZoneCenterBroadcastIngestor" /> already uses). Contract:
///     scratchpad/contracts/wave13/A5-zone051-053-states.md.
/// </remarks>
public static class Zone051Zone053BroadcastResolver
{
    /// <summary>Zone051 family selector range (S04_MyWork02.cpp:254-289).</summary>
    public const int Zone051RangeStart = 10;

    public const int Zone051RangeEnd = 18;

    /// <summary>Zone053 family selector range (S04_MyWork02.cpp:290-335).</summary>
    public const int Zone053RangeStart = 19;

    public const int Zone053RangeEnd = 30;

    /// <summary>
    ///     Selectors 10-18: reads the zone-slot index from the first 4 bytes of <paramref name="data" /> and
    ///     writes the mapped state via <see cref="Zone051Zone053StateMap.TryMapZone051" /> (selector 10 maps
    ///     to nothing and performs no write, matching the family's block-opening no-op case). Caller must
    ///     guarantee <paramref name="data" /> is at least 4 bytes long -- matches every sibling
    ///     <c>ApplyZoneNNN</c> method's own assumption in <see cref="ZoneCenterBroadcastIngestor" />, which
    ///     itself only ever calls in with the full 130-byte payload.
    /// </summary>
    public static void ApplyZone051(Zone051Zone053SiegeState state, int selector, ReadOnlySpan<byte> data,
        ILogger logger)
    {
        var slot = ReadInt32(data, 0);
        if (!Zone051Zone053SiegeState.IsValidZone051Slot(slot))
        {
            logger.LogWarning("Zone051 selector {Selector} referenced out-of-range slot {Slot} -- ignored",
                selector, slot);
            return;
        }

        if (Zone051Zone053StateMap.TryMapZone051(selector, out var mapped))
            state.SetZone051(slot, mapped);
    }

    /// <summary>
    ///     Selectors 19-30: reads the zone-slot index from the first 4 bytes of <paramref name="data" /> and
    ///     writes the mapped state via <see cref="Zone051Zone053StateMap.TryMapZone053" /> (selector 19's
    ///     intended write is dead code in every shipped build and never applies here either -- see
    ///     <see cref="Zone051Zone053StateMap.TryMapZone053" />'s own remarks; selectors 25-27 are bare no-ops).
    ///     Caller must guarantee <paramref name="data" /> is at least 4 bytes long, same assumption as
    ///     <see cref="ApplyZone051" />.
    /// </summary>
    public static void ApplyZone053(Zone051Zone053SiegeState state, int selector, ReadOnlySpan<byte> data,
        ILogger logger)
    {
        var slot = ReadInt32(data, 0);
        if (!Zone051Zone053SiegeState.IsValidZone053Slot(slot))
        {
            logger.LogWarning("Zone053 selector {Selector} referenced out-of-range slot {Slot} -- ignored",
                selector, slot);
            return;
        }

        if (Zone051Zone053StateMap.TryMapZone053(selector, out var mapped))
            state.SetZone053(slot, mapped);
    }

    private static int ReadInt32(ReadOnlySpan<byte> data, int offset)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
    }
}
