using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Progression;

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
///     single Tower point. Every one of the twelve <see cref="AntiCampingGuardPointCatalog.GuardedMapIds" />
///     has exactly one Tower point in the recovered legacy data (<see cref="AntiCampingGuardPointCatalog.Default" />)
///     -- <see cref="TowerPoint" /> stays nullable only so a caller-supplied, deliberately partial catalog
///     (tests, <see cref="AntiCampingGuardPointCatalog.Empty" />) can still omit it.
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
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:1739 (<c>MyGame::Logic</c> -- encloses the Symbol Point
///     switch below) ; :2074-2371 (Symbol Point switch, gated by <c>FIX_HSB_POS_BUG</c>, keyed on the zone
///     process's own server number -- the twelve case literals reproduced in <see cref="GuardedMapIds" /> and
///     <see cref="Default" /> live at :2079-2368, closing brace at :2371) ; :2373-2406 (the same switch's
///     immediately following Tower proximity addendum, evaluated only when no Symbol Point already flagged
///     the avatar this tick, at a 40-unit threshold) ; :2407-2426 (a further, entirely commented-out
///     thirteenth <c>case</c> for zone/server 196, referencing a separate battle-post location field -- dead,
///     non-functional code, correctly excluded from <see cref="Default" />) ; :425 (<c>MyGame::Init</c> --
///     encloses the Tower Point switch below, run once at zone-server start-up, never recomputed per tick) ;
///     :1329-1356 (Tower Point per-map switch on the same server-number list, the twelve case literals at
///     :1341-1352, default at :1353-1355 disabling the Tower guard for any other map) ;
///     Server/ts25zone/H07_MyGame.h:416 (<c>mTowerLocation</c>, the three-element float array this switch
///     writes once at start-up and every later read -- including <see cref="TowerGuardianCatalog" />'s own
///     :1341-1352 citation for the guardian monster's stand point, and
///     Server/ts25zone/S10_MySummon.cpp:2209 and Server/ts25zone/S04_MyWork03.cpp:7532's own reads of the
///     same field -- observes unchanged) ; Server/ts25zone/H07_MyGame.h:57-59 (<c>FIX_HSB_POS_BUG</c> defined
///     only under <c>LNW33</c>) cross-checked against Server/Header/Protocol/DEFINE.h:21-23 (<c>LNW33</c>
///     defined only under <c>M33</c>) -- <c>M33</c> is the active variant in the one real buildable
///     configuration (<c>ReleaseEU33</c>), so <c>FIX_HSB_POS_BUG</c> is live in production, not a dead or
///     variant-only branch ; Server/Header/Protocol/DEFINE.h:86 (<c>USE_TOWER</c> defined unconditionally,
///     outside any variant-gated block -- the Tower guard is also live unconditionally).
///     <para>
///         <b>Tower Point reuses <see cref="TowerGuardianCatalog" /> rather than re-transcribing a second
///         copy of the same twelve-row table.</b> <c>mTowerLocation</c> is a single process-wide field,
///         written once at start-up (S07_MyGame01.cpp:1329-1356) and read unchanged by every later consumer --
///         this mechanism's own Tower proximity check, the tower guardian's monster-spawn placement
///         (<see cref="TowerGuardianCatalog.TryGetGuardianLocation" />, citing the identical
///         S07_MyGame01.cpp:1341-1352 range), and a separate 200-unit restricted-area item-use gate
///         (S04_MyWork03.cpp:7532, out of scope here). Re-deriving a second literal table here would risk the
///         two silently drifting apart; <see cref="TowerZoneIndexTable.GetTowerIndex" />'s own twelve-map set
///         is identical to <see cref="GuardedMapIds" />, so every guarded map here also resolves a Tower point
///         there.
///     </para>
///     <para>
///         <b>Symbol Point count is not uniform across maps</b> (inherent to the source data, not an
///         omission): four maps (2, 7, 12, 141) get exactly one ; four maps (3, 8, 13, 142) get exactly three
///         ; four maps (4, 9, 14, 143) get exactly one, from a different literal group than the first four.
///         The legacy switch tags each Symbol Point case with its own special-type marker (11, 12, 13, or 28)
///         to distinguish which symbol slot a point represents -- <b>deliberately not modeled here</b>, since
///         <see cref="AntiCampingForcedReturnSystem" /> already evaluates every configured Symbol Point in
///         plain array order regardless of which slot fired (its own remarks: only the last-evaluated point's
///         outcome survives a tick), so the marker carries no behavior this reimplementation needs to
///         observe.
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

    /// <summary>
    ///     Every guarded map present, but with no coordinate data configured for any of them -- a deliberately
    ///     inert catalog for tests/callers that want the guarded-map gating without the real coordinates. See
    ///     <see cref="Default" /> for the recovered, production-ready data.
    /// </summary>
    public static readonly AntiCampingGuardPointCatalog Empty =
        new(ImmutableDictionary<short, AntiCampingMapGuardPoints>.Empty);

    /// <summary>
    ///     The Holy Stone Symbol Point literal table, transcribed from
    ///     Server/ts25zone/S07_MyGame01.cpp:2079-2368 (the twelve live cases of the Symbol Point switch cited
    ///     on the class remarks). Points within a map are listed in the exact switch-case order the legacy
    ///     evaluates them in -- see <see cref="AntiCampingForcedReturnSystem" /> for why that order matters.
    ///     <b>Declared before <see cref="Default" /> so its own static-initializer order is guaranteed ready</b>
    ///     when <see cref="BuildDefault" /> reads it (C# runs static field initializers in textual order).
    /// </summary>
    private static readonly ImmutableDictionary<short, ImmutableArray<AntiCampingGuardPoint>> HolyStoneSymbolPointTable =
        new Dictionary<short, ImmutableArray<AntiCampingGuardPoint>>
        {
            [2] = [new AntiCampingGuardPoint(-1810f, -1f, 3155f)],
            [3] =
            [
                new AntiCampingGuardPoint(-6760f, 0f, 1187f),
                new AntiCampingGuardPoint(-7780f, 0f, 400f),
                new AntiCampingGuardPoint(-6864f, 0f, 2761f)
            ],
            [4] = [new AntiCampingGuardPoint(7839f, 461f, 6520f)],
            [7] = [new AntiCampingGuardPoint(-831f, 10f, -3392f)],
            [8] =
            [
                new AntiCampingGuardPoint(4410f, 28f, 4666f),
                new AntiCampingGuardPoint(5493f, 38f, 4174f),
                new AntiCampingGuardPoint(5545f, 41f, 6452f)
            ],
            [9] = [new AntiCampingGuardPoint(-2438f, -590f, 6697f)],
            [12] = [new AntiCampingGuardPoint(-4045f, 0f, 1648f)],
            [13] =
            [
                new AntiCampingGuardPoint(-7610f, 0f, 5763f),
                new AntiCampingGuardPoint(-6684f, 0f, 5319f),
                new AntiCampingGuardPoint(-5397f, 0f, 5819f)
            ],
            [14] = [new AntiCampingGuardPoint(7174f, 336f, 6191f)],
            [141] = [new AntiCampingGuardPoint(-1132f, 0f, 3486f)],
            [142] =
            [
                new AntiCampingGuardPoint(-2505f, 0f, 7201f),
                new AntiCampingGuardPoint(-2063f, 1f, 6846f),
                new AntiCampingGuardPoint(-2948f, 8f, 6105f)
            ],
            [143] = [new AntiCampingGuardPoint(-38f, 0f, 4432f)]
        }.ToImmutableDictionary();

    /// <summary>
    ///     The fully recovered, production-ready catalog: every one of the twelve <see cref="GuardedMapIds" />
    ///     resolves both its Holy Stone Symbol Point(s) and its Tower Point (via
    ///     <see cref="TowerGuardianCatalog" />, see this class's own remarks). Built once and never mutated
    ///     after.
    /// </summary>
    public static readonly AntiCampingGuardPointCatalog Default = BuildDefault();

    private static AntiCampingGuardPointCatalog BuildDefault()
    {
        var builder = ImmutableDictionary.CreateBuilder<short, AntiCampingMapGuardPoints>();

        foreach (var mapId in GuardedMapIds)
        {
            var symbolPoints = HolyStoneSymbolPointTable[mapId];
            AntiCampingGuardPoint? towerPoint =
                TowerGuardianCatalog.TryGetGuardianLocation(mapId, out var x, out var y, out var z)
                    ? new AntiCampingGuardPoint(x, y, z)
                    : null;

            builder[mapId] = new AntiCampingMapGuardPoints(symbolPoints, towerPoint);
        }

        return new AntiCampingGuardPointCatalog(builder.ToImmutable());
    }

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
