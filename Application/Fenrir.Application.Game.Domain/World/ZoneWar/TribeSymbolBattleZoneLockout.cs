namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     The unconditional, per-player-INDEPENDENT zone-transfer lockout tied to an active tribe-symbol-battle
///     event (<c>S04_MyWork02.cpp:2125-2141</c>): while the battle window is open, any request moving from
///     zone 40, 41, or 42 into zone 38 specifically is disconnected outright, regardless of the requester's
///     tribe, alliance, or GM rank. This is a distinct anti-exploit/anti-flee rule gating one specific
///     zone-pair direction tied to the world event, not a capital-zone/tribe-ownership check.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:2125-2141 -- <c>#ifdef LNW33</c>-gated, unconditional (no
///     per-player flag) check that the tribe-symbol-battle event is active, switched on the zone process's own
///     server number being 40, 41, or 42, blocking a request whose destination is exactly zone 38 with an
///     immediate disconnect and bare return (no response constructed). <c>LNW33</c> is active in the real
///     production <c>ReleaseEU33</c> build per this codebase's own build-variant citation-discipline note, so
///     this is live production behavior, not a dead branch.
///     <para>
///         Structurally distinct from, and evaluated strictly AFTER, the separate
///         <c>mProtect_ReviveHack</c>-gated capital-zone/tribe-ownership check
///         (Server/ts25zone/S04_MyWork02.cpp:2019-2064, source zones 1-4/6-9/11-14/140-143 with an explicit
///         destination-38 carve-out) -- neither check substitutes for or disables the other. Also evaluated
///         after the same-zone no-op check, the valid-zone-number-range/present-zone check, and the
///         per-<c>tSort</c>-reason switch that precede it in the same handler
///         (Server/ts25zone/S04_MyWork02.cpp:2065-2114) -- none of those earlier gates skip or bypass this one
///         for any surviving <c>tSort</c> reason, including a GM-initiated move.
///     </para>
/// </remarks>
public static class TribeSymbolBattleZoneLockout
{
    /// <summary>The one destination zone number this rule reacts to.</summary>
    public const short GuardedDestinationZoneId = 38;

    /// <summary>The three source zone numbers this rule reacts to -- any other source zone is unaffected.</summary>
    public static readonly IReadOnlySet<short> GuardedSourceZoneIds = new HashSet<short> { 40, 41, 42 };

    /// <summary>
    ///     True when a zone-transfer request from <paramref name="sourceZoneId" /> to
    ///     <paramref name="destinationZoneId" /> must be disconnected outright because a tribe-symbol-battle
    ///     event is currently open (<paramref name="tribeSymbolBattleActive" />). Independent of tribe,
    ///     alliance, and GM/operator rank -- see class remarks.
    /// </summary>
    public static bool IsLockedOut(short sourceZoneId, short destinationZoneId, bool tribeSymbolBattleActive)
    {
        return tribeSymbolBattleActive &&
               destinationZoneId == GuardedDestinationZoneId &&
               GuardedSourceZoneIds.Contains(sourceZoneId);
    }
}
