namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     Concrete <see cref="IPersonalDungeonBossCatalog" /> supplying the rebirth-tier -&gt; boss-catalog-id
///     table for <see cref="Zone.TryEnterZone241PersonalInstance" />, filling the gap
///     <see cref="NullPersonalDungeonBossCatalog" />'s own remarks describe ("no tier ever resolves ... until
///     a real catalog is supplied").
/// </summary>
/// <remarks>
///     <para>
///         Backed by <see cref="PersonalDungeonBossTables.ResolveCatalogE" />, NOT
///         <see cref="PersonalDungeonBossTables.ResolveCatalogA" /> -- even though
///         <see cref="IPersonalDungeonBossCatalog" />'s own pre-existing doc comment describes its single
///         method as "the zone-enter-handler table's Fenrir equivalent" (Catalog A). The A4-missing-bosses
///         contract this class was implemented from independently re-verified a three-way summon race (see
///         <see cref="PersonalDungeonBossTables" />'s own class remarks) whose confirmed net effect is that
///         Catalog E's value -- not Catalog A's -- is what a player actually ends up facing for every rebirth
///         tier, on every Zone241 server. Given <see cref="Zone.TryEnterZone241PersonalInstance" />'s existing
///         architecture only ever makes ONE summon call per entry (it does not reproduce the underlying
///         two-or-three-step invalidate-then-respawn race as separate ticks), resolving directly to the race's
///         confirmed FINAL outcome (Catalog E) is the closer legacy-faithful behavior for a single-call
///         architecture, not an arbitrary substitution -- see the open question in
///         <see cref="PersonalDungeonBossTables" />'s remarks about whether the intermediate race steps are
///         separately client-observable before this is revisited.
///     </para>
///     <para>
///         <b>Deliberately NOT modeled here:</b> Catalog D's server-325-330-specific table/position/LOD-round
///         consumption -- Catalog D's own transient spawn is invalidated by the race before a player can ever
///         actually face it (see <see cref="PersonalDungeonBossTables" /> remarks), so it has no bearing on
///         which boss id this catalog should resolve to; only its LOD-round-consumption SIDE EFFECT would need
///         separate modeling, and that requires new state-machine work this catalog (a pure id lookup) is not
///         the place for -- see this cluster's wiring report.
///     </para>
/// </remarks>
public sealed class Zone241RebirthTierBossCatalog : IPersonalDungeonBossCatalog
{
    public static readonly Zone241RebirthTierBossCatalog Instance = new();

    private Zone241RebirthTierBossCatalog()
    {
    }

    /// <summary>
    ///     Always resolves (every rebirth tier, including values outside 0-12, maps to an explicit or
    ///     fallback entry in <see cref="PersonalDungeonBossTables.ResolveCatalogE" />) -- unlike
    ///     <see cref="NullPersonalDungeonBossCatalog" />, this catalog never reports a summon failure.
    /// </summary>
    public bool TryGetBossMonsterId(int rebirthTier, out int monsterId)
    {
        monsterId = PersonalDungeonBossTables.ResolveCatalogE(rebirthTier);
        return true;
    }
}
