using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.Monsters;

/// <summary>
///     Injected, immutable per-zone boss-candidate pool for <see cref="MonsterBossSpawnSystem" />, keyed by
///     physical map id. The Fenrir replacement for legacy's boot-time per-zone boss-region file load
///     (<c>LoadRegionInfo_2</c> / <c>SetSummonBossInfo</c>, <c>Server/ts25zone/S10_MySummon.cpp:600-610</c>).
/// </summary>
/// <remarks>
///     <para>
///         Defaults to <see cref="Empty" /> -- every map's pool is empty, so the whole <c>SummonBossMonster</c>
///         machine is inert on every zone, matching its own "candidate count below 1 =&gt; machine never spawns"
///         precondition (<c>Server/ts25zone/S10_MySummon.cpp:1068-1071</c>). This is the honest default given
///         the source contract's flagged OPEN QUESTION: the concrete boss ids per zone cannot be certified from
///         the committed repository. The compiled loader reads a <c>.csv</c> the shipped data directory does not
///         contain (only the binary <c>.WREGION</c> files ship, <c>Server/BuildEU33/DATA/SUMMON/WRegion/</c>),
///         so against committed data the load yields an empty pool and the machine is a silent no-op. A real
///         catalog must come from a verified out-of-band data pipeline; DO NOT invent boss ids to populate this.
///     </para>
///     <para>
///         Keyed by PHYSICAL map id, NOT the legacy canonical (<c>mSameSummon</c>-remapped) boss-file number:
///         that spawn-side remap (<c>S10_MySummon.cpp:88-360</c>) diverges from the geometry-side
///         <see cref="Geometry.ZoneCanonicalGeometryMap" /> and is itself an unrecovered open item, so the data
///         pipeline that builds this catalog owns expanding a canonical boss set to its physical maps; this class
///         only stores the already-resolved per-physical-map pools.
///     </para>
///     <para>
///         Deliberately a SEPARATE pool from <see cref="MonsterSpawnScheduler" />'s <c>world.MonsterSpawnRegions</c>
///         table. In the current Fenrir schema the boss-file rows already ride in that region table and are
///         population-spawned by the generic scheduler (see <see cref="MonsterSpawnScheduler" />'s class
///         remarks); sourcing this machine from those same rows would DOUBLE-SPAWN them. Keeping this catalog
///         empty-by-default and independent is what prevents that -- a real boss catalog supplied here must be
///         for bosses NOT already seeded into <c>world.MonsterSpawnRegions</c>.
///     </para>
/// </remarks>
public sealed class MonsterBossSummonCatalog
{
    private readonly FrozenDictionary<short, ImmutableArray<MonsterBossSummonCandidate>> _byMapId;

    public MonsterBossSummonCatalog(IReadOnlyDictionary<short, ImmutableArray<MonsterBossSummonCandidate>> byMapId)
    {
        _byMapId = byMapId.ToFrozenDictionary();
    }

    /// <summary>An all-empty catalog: the <c>SummonBossMonster</c> machine is inert on every map. The production default.</summary>
    public static MonsterBossSummonCatalog Empty { get; } =
        new(FrozenDictionary<short, ImmutableArray<MonsterBossSummonCandidate>>.Empty);

    /// <summary>
    ///     The boss-candidate pool for <paramref name="mapId" />, or an empty (never default) array when this map
    ///     ships no boss file -- the overwhelming majority, and every map under the <see cref="Empty" /> default.
    /// </summary>
    public ImmutableArray<MonsterBossSummonCandidate> CandidatesFor(short mapId)
    {
        return _byMapId.TryGetValue(mapId, out var pool) ? pool : [];
    }

    /// <summary>
    ///     Applies the legacy boss-only load-time even-count rule (<c>S10_MySummon.cpp:485-493</c>): if the loaded
    ///     row count is odd, the LAST row is silently dropped; if that leaves fewer than 1 row, the whole pool is
    ///     discarded (returned empty), making the machine inert for that zone. Ordinary-monster loads do NOT apply
    ///     this rule -- it is boss-load-specific. Exposed for a data-pipeline builder to normalize each zone's
    ///     rows before constructing the catalog; the <see cref="Empty" /> default never exercises it.
    /// </summary>
    public static ImmutableArray<MonsterBossSummonCandidate> NormalizeBossRows(
        IReadOnlyList<MonsterBossSummonCandidate> rows)
    {
        var count = rows.Count;
        if ((count & 1) == 1)
            count--; // odd row count: drop the trailing row

        if (count < 1)
            return []; // fewer than 1 row survives -> discard the whole pool (machine stays inert)

        var builder = ImmutableArray.CreateBuilder<MonsterBossSummonCandidate>(count);
        for (var i = 0; i < count; i++)
            builder.Add(rows[i]);
        return builder.MoveToImmutable();
    }
}
