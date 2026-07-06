using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     One hardcoded three-axis world coordinate FIX_HSB_POS_BUG's proximity check compares an avatar's
///     position against -- the full X/Y/Z coordinate, not a horizontal-only (X/Z) one.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/Header/mapcheck.h:12-15 (<c>GetLengthXYZ</c> -- confirms every proximity check in
///     this mechanism is a full three-axis Euclidean distance).
/// </remarks>
public readonly record struct AntiCampingGuardPoint(float X, float Y, float Z);

/// <summary>
///     One guarded map's fixed evaluation set: the map's Holy Stone symbol point(s) (one to three, checked
///     in this exact array order every tick -- order matters, since only the last-evaluated point's outcome
///     survives a given tick, see <see cref="AntiCampingForcedReturnSystem" />'s own remarks) plus its
///     optional single Tower point (present only on the subset of guarded maps that have a Tower).
/// </summary>
public sealed record AntiCampingMapGuardPoints(
    ImmutableArray<AntiCampingGuardPoint> HolyStoneSymbolPoints,
    AntiCampingGuardPoint? TowerPoint)
{
    /// <summary>No points configured for this map -- every evaluation against it is a safe no-op.</summary>
    public static readonly AntiCampingMapGuardPoints Empty = new([], null);
}

/// <summary>
///     FIX_HSB_POS_BUG's per-map guarded-point table: which server numbers this mechanism evaluates at all,
///     and (for each) the Holy Stone symbol/Tower coordinates to check proximity against.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:2074-2371 (first switch, keyed on the zone process's own
///     server number -- enumerates the fixed guarded server numbers reproduced in
///     <see cref="GuardedMapIds" /> and, per server number, one to three hardcoded Holy Stone symbol
///     coordinates) ; :2373-2406 (second switch, the Tower proximity addendum, keyed on the same fixed
///     server-number list -- only a further, separately-enumerated subset of <see cref="GuardedMapIds" />
///     actually has a case there, i.e. a <see cref="AntiCampingMapGuardPoints.TowerPoint" />) ; :2407-2426 (a
///     further <c>case</c> in that same second switch is entirely commented out -- a dead, non-functional
///     zone-195 variant that must not be treated as production data).
///     <para>
///         GAP -- not modeled, deliberately not guessed: the concrete per-map literal float coordinates (both
///         the Holy Stone symbol points and the Tower point) are hardcoded in the cited legacy switches and
///         are NOT reproduced here -- the translated behavior contract intentionally carries no numeric
///         coordinate values, only the shape and the fixed server-number list. <see cref="Empty" /> makes
///         every guarded map a documented no-op (points list empty, no Tower) until a follow-up data-gathering
///         pass transcribes the literal per-map table from S07_MyGame01.cpp:2074-2406 -- the same posture
///         already established by <see cref="TribeSymbolCatalog" />/<see cref="GuardPostCatalog" /> for their
///         own hardcoded-coordinate switches.
///     </para>
/// </remarks>
public sealed class AntiCampingGuardPointCatalog(IReadOnlyDictionary<short, AntiCampingMapGuardPoints> pointsByMapId)
{
    /// <summary>
    ///     The fixed, explicit set of server numbers FIX_HSB_POS_BUG evaluates at all -- every other server
    ///     number makes this entire mechanism inert (Server/ts25zone/S07_MyGame01.cpp:2074-2371's own switch
    ///     labels).
    /// </summary>
    public static readonly ImmutableArray<short> GuardedMapIds = [2, 3, 4, 7, 8, 9, 12, 13, 14, 141, 142, 143];

    /// <summary>Every guarded map present, but with no coordinate data configured for any of them -- see this class's own GAP remarks.</summary>
    public static readonly AntiCampingGuardPointCatalog Empty = new(ImmutableDictionary<short, AntiCampingMapGuardPoints>.Empty);

    /// <summary>
    ///     Whether <paramref name="mapId" /> is one of the fixed <see cref="GuardedMapIds" /> -- a map outside
    ///     this set is never touched by this mechanism at all, regardless of what (if anything) is configured
    ///     for it in the backing dictionary.
    /// </summary>
    public bool IsGuardedMap(short mapId)
    {
        return GuardedMapIds.Contains(mapId);
    }

    /// <summary>
    ///     The configured guard points for <paramref name="mapId" />, or <see cref="AntiCampingMapGuardPoints.Empty" />
    ///     if none are configured -- callers must still gate on <see cref="IsGuardedMap" /> first, since an
    ///     unguarded map with (hypothetically) configured points must still be treated as fully inert.
    /// </summary>
    public AntiCampingMapGuardPoints GetPoints(short mapId)
    {
        return pointsByMapId.TryGetValue(mapId, out var points) ? points : AntiCampingMapGuardPoints.Empty;
    }
}
