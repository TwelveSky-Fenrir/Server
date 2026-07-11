using System.Collections.Frozen;

namespace Fenrir.Application.Game.GameData;

/// <summary>
///     The canonical spawn-region zone id and <c>_FIX</c>-suffix flag for one physical zone number, per
///     <see cref="ZoneCanonicalSpawnRegionMap.Resolve" />.
/// </summary>
public readonly record struct SpawnRegionCanonicalZone(short CanonicalZoneId, bool UsesFixSuffix);

/// <summary>
///     Physical-&gt;canonical zone remap for monster spawn-region (<c>.WREGION</c>) loading, plus the
///     <c>_FIX</c> filename-suffix override -- a partial port of the <c>switch (mSERVER_INFO.mServerNumber)</c>
///     in legacy <c>MySummon::Init</c> (<c>Server/ts25zone/S10_MySummon.cpp:88-360</c>, canonical target
///     <c>mSameSummon</c>).
/// </summary>
/// <remarks>
///     <para>
///         Behavior contract: <c>wave11/A1-canonical-wm-remap.md</c>. This is a STRUCTURALLY SEPARATE
///         mechanism from <c>Fenrir.Application.Game.Domain.World.Geometry.ZoneCanonicalGeometryMap</c>'s
///         <c>.WM</c> navmesh remap, even though both are keyed by the same physical zone number and both run
///         once at <c>ts25zone</c> boot (navmesh first, then spawn regions --
///         <c>Server/ts25zone/S07_MyGame01.cpp:1441</c> before <c>:1658</c>). The two canonical targets diverge
///         for some ids: the concrete example the contract calls out is physical zones 39/144/145/313, which
///         fold into navmesh canonical 126 (<c>ZoneCanonicalGeometryMap</c>'s <c>LNW33</c> branch,
///         <c>S09_MyWorld.cpp:305-313</c>) but keep their OWN zone number for spawn purposes, gaining only the
///         <c>_FIX</c> filename suffix (<c>S10_MySummon.cpp:91-99</c>). Never infer one table's grouping from
///         the other's.
///     </para>
///     <para>
///         WHY THIS MATTERS for Fenrir specifically (established by reading the DB import pipeline, not
///         stated by the contract itself): the on-disk <c>*.WREGION.csv</c> file set has exactly one file per
///         canonical <c>mSameSummon</c> value (filename construction at <c>S10_MySummon.cpp:364,368</c>), and
///         Fenrir's importer (<c>Tools/Fenrir.Tools.LegacyDataImport/Legacy/Readers/MonsterSpawnRegionReader.cs</c>,
///         <c>ZoneNumberPattern</c> = <c>^Z(\d+)_</c>) parses each row's
///         <c>world.MonsterSpawnRegions.ZoneNumber</c> straight from that file's own leading "Z0NN_" segment
///         -- i.e. from the CANONICAL zone number, not from every physical zone that shares it.
///         <see cref="Fenrir.Application.Game.GameData.WorldDataCacheBuilder.BuildZones" /> uses
///         <see cref="ResolveCanonicalSpawnZoneId" /> to remap each <c>ZoneDefinition</c>'s own physical
///         <c>ZoneNumber</c> before the spawn-region lookup, so a physical zone that is not its own canonical
///         spawn zone (e.g. 102, which shares canonical 101 with 103/167) still resolves to the seeded spawn
///         regions filed under zone 101's rows instead of an empty list.
///     </para>
///     <para>
///         <b>Layering note:</b> this type lives in <c>Fenrir.Application.Game.GameData</c>, not
///         <c>Fenrir.Application.Game.Domain</c> (unlike its sibling <c>ZoneCanonicalGeometryMap</c>), because
///         its only real consumer, <see cref="Fenrir.Application.Game.GameData.WorldDataCacheBuilder" />, lives
///         in this lower layer -- <c>Fenrir.Application.Game.Domain</c> references
///         <c>Fenrir.Application.Game.GameData</c>, never the reverse, so a Domain-layer type can never be
///         called from here.
///     </para>
///     <para>
///         The <c>_FIX</c> suffix, by contrast, is INERT for that exact DB-lookup concern: it is appended
///         after the "Z0NN_" prefix the importer's regex anchors on, so a <c>_FIX</c> zone's <c>ZoneNumber</c>
///         column parses identically whether or not the suffix is present. <see cref="UsesFixSuffix" /> is
///         still exposed for legacy-parity completeness (the contract explicitly asks for the mechanism), not
///         because any known Fenrir consumer needs to branch on it today.
///     </para>
///     <para>
///         INCOMPLETE DATA -- the contract's own Table B section states several canonical groups only by their
///         canonical anchor id and a cardinality description ("three-zone ND/RS/GT triples", "four physical
///         zones each"), never the actual member-zone list (contract lines 63-69, itself carried forward from
///         an earlier extraction pass that did not re-open
///         <c>
///             S10_MySummon.cpp:119-165, 174-228 (partially --
///             126-129 in that range ARE fully specified, see below), 258-329, 331-350
///         </c>
///         this session). The
///         following canonical anchors are therefore NOT implemented below, pending a follow-up
///         <c>cpp-zone-gameplay-analyst</c> extraction confirming exact membership -- do not guess at them by
///         analogy with <c>ZoneCanonicalGeometryMap</c>'s grouping, since the two tables are documented to
///         diverge:
///         <list type="bullet">
///             <item>40, 43, 46, 56, 59, 62, 63, 64 (three-zone ND/RS/GT triples -- <c>:119-165</c>)</item>
///             <item>104, 105, 110, 111, 251, 252, 259, 260 (four-zone groups -- <c>:174-228</c>)</item>
///             <item>175, 178, 182, 186, 190, 19, 25, 31 (Labyrinth sub-variants, four zones each -- <c>:258-307</c>)</item>
///             <item>210, 211, 212 (Temple Exterior 1/2/3, four variants each -- <c>:310-329</c>)</item>
///             <item>222, 223, 224 (Temple Interior 1/2/3, four variants each -- <c>:331-350</c>)</item>
///         </list>
///         Any physical zone belonging to one of these unmapped groups falls through to this map's own default
///         (identity) via <see cref="ResolveCanonicalSpawnZoneId" /> -- a known-incorrect-relative-to-legacy
///         but safe fallback that never invents membership data. Physical zones in these groups will keep
///         resolving to an empty (or wrong) spawn-region list until the extraction gap above is closed.
///     </para>
/// </remarks>
public static class ZoneCanonicalSpawnRegionMap
{
    /// <summary>
    ///     The five zones whose spawn-region filename gains a literal "_FIX" suffix
    ///     (<c>Server/ts25zone/S10_MySummon.cpp:91-99</c>) WITHOUT remapping their canonical zone number --
    ///     the selector sets the suffix and exits immediately, before any value-changing group can run.
    /// </summary>
    private static readonly FrozenSet<short> FixSuffixZones =
        new short[] { 39, 144, 145, 313, 74 }.ToFrozenSet();

    private static readonly FrozenDictionary<short, short> CanonicalRedirects = BuildCanonicalRedirects();

    /// <summary>
    ///     Returns the canonical <c>mSameSummon</c> zone id whose <c>world.MonsterSpawnRegions.ZoneNumber</c>
    ///     rows back <paramref name="physicalZoneId" />'s spawn regions, or <paramref name="physicalZoneId" />
    ///     itself when it is its own canonical zone -- the legacy switch's <c>default</c>, one of the 5
    ///     <see cref="UsesFixSuffix" /> zones (which never remap their own number, see remarks), or a zone
    ///     belonging to one of the not-yet-extracted groups documented on this type's own remarks.
    /// </summary>
    public static short ResolveCanonicalSpawnZoneId(short physicalZoneId)
    {
        return CanonicalRedirects.GetValueOrDefault(physicalZoneId, physicalZoneId);
    }

    /// <summary>
    ///     True for the 5 zones whose legacy <c>.WREGION</c> regular-monster filename carries a literal
    ///     <c>_FIX</c> suffix (<c>Server/ts25zone/S10_MySummon.cpp:91-99</c>). The boss-monster filename
    ///     pattern has no suffix segment at all, for any zone (<c>:368</c>) -- this flag describes only the
    ///     regular-monster file. See this type's own remarks for why the suffix does not affect Fenrir's
    ///     DB-backed spawn-region lookup.
    /// </summary>
    public static bool UsesFixSuffix(short physicalZoneId)
    {
        return FixSuffixZones.Contains(physicalZoneId);
    }

    /// <summary>
    ///     Combined canonical-zone-id + <c>_FIX</c>-suffix resolution for <paramref name="physicalZoneId" />,
    ///     for a single call site that wants both (e.g. a spawn-region DB lookup keyed by
    ///     <see cref="SpawnRegionCanonicalZone.CanonicalZoneId" /> alongside a diagnostic/log line reporting
    ///     <see cref="SpawnRegionCanonicalZone.UsesFixSuffix" />).
    /// </summary>
    public static SpawnRegionCanonicalZone Resolve(short physicalZoneId)
    {
        return new SpawnRegionCanonicalZone(ResolveCanonicalSpawnZoneId(physicalZoneId), UsesFixSuffix(physicalZoneId));
    }

    private static FrozenDictionary<short, short> BuildCanonicalRedirects()
    {
        var map = new Dictionary<short, short>();

        void Group(short canonical, params short[] physicals)
        {
            foreach (var physical in physicals)
                map[physical] = canonical;
        }

        // S10_MySummon.cpp:101-117 -- fully specified in the contract, independently re-read this session.
        Group(16, 22, 28);
        Group(17, 23, 29);
        Group(18, 24, 30);

        // S10_MySummon.cpp:167-172 -- fully specified.
        Group(101, 102, 103, 167);

        // S10_MySummon.cpp:230-256 -- fully specified (the 4 sub-groups of the 126-129 block). Note the
        // divergence from ZoneCanonicalGeometryMap's navmesh table: THIS table does not fold 39/144/145/313
        // into 126 the way the navmesh table does -- those 4 zones stay at their own number here (see
        // FixSuffixZones above) and are deliberately absent from this group.
        Group(126, 130, 134, 171);
        Group(127, 131, 135, 172);
        Group(128, 132, 136, 173);
        Group(129, 133, 137, 174);

        // S10_MySummon.cpp:351-353 -- fully specified.
        Group(310, 336);

        // S10_MySummon.cpp:355-357 -- canonical 344 self-maps to {344} only ("a self-mapping, explicitly
        // listed even though it changes nothing" per the contract); a genuine no-op, omitted here, same
        // "identity mappings are omitted" convention ZoneCanonicalGeometryMap.ResolveCanonicalMapId documents.

        // NOT implemented -- the contract gives only the canonical anchor id and a cardinality description for
        // these groups, never the actual physical-zone membership (contract lines 63-69). Do not add entries
        // here without a follow-up extraction confirming exact membership -- see this type's own remarks.
        //   40, 43, 46, 56, 59, 62, 63, 64          (S10_MySummon.cpp:119-165)
        //   104, 105, 110, 111, 251, 252, 259, 260  (S10_MySummon.cpp:174-228)
        //   175, 178, 182, 186, 190, 19, 25, 31     (S10_MySummon.cpp:258-307)
        //   210, 211, 212                            (S10_MySummon.cpp:310-329)
        //   222, 223, 224                            (S10_MySummon.cpp:331-350)

        return map.ToFrozenDictionary();
    }
}
