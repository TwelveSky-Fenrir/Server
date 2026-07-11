namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     Zone-241 "LOD" personal-boss-chain dungeon: the three divergent rebirth-tier/server-number -&gt;
///     boss-catalog-id lookup tables legacy hardcodes at three separate call sites, plus their two distinct
///     fixed summon positions. See <c>Zone.DungeonInstance.cs</c>/<see cref="IPersonalDungeonBossCatalog" />
///     for the (currently single-call-site) consumer, and <see cref="Zone241RebirthTierBossCatalog" /> for the
///     concrete <see cref="IPersonalDungeonBossCatalog" /> implementation built from
///     <see cref="CatalogE" /> below.
/// </summary>
/// <remarks>
///     <para>
///         Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:1142-1191 (Catalog A -- fires on every zone-entry for any
///         Zone241 server) ; Server/ts25zone/S07_MyGame03.cpp:6260-6339 (Catalog D -- fires only on servers
///         325-330, gated on at least one unspent LOD round) ; Server/ts25zone/S07_MyGame01.cpp:11755-11901
///         (Catalog E -- fires on every subsequent zone tick while the avatar's instance state equals 2).
///     </para>
///     <para>
///         <b>Confirmed three-way summon race (A4-missing-bosses contract, independently re-verified):</b> on
///         servers 325-330, Catalog D runs first (inside registration validation, called earlier in the same
///         registration handler) and advances instance state to 3; Catalog A then runs later in the very same
///         handler call, unconditionally, invalidating Catalog D's spawn and resetting instance state back to
///         2; because instance state is back at 2, the *next* zone tick's Catalog E block fires, invalidating
///         Catalog A's spawn and summoning yet again per <see cref="CatalogE" />. <b>Net result: Catalog E's
///         value is what a player actually ends up facing for rebirth tiers 2 through 5 on every Zone241
///         server, including 325 through 330</b> -- not Catalog A's or Catalog D's, even though those run
///         "first" within the same handler. On the fourteen Zone241 servers other than 325-330 the race is only
///         two-way (Catalog A, then Catalog E on the next tick) since Catalog D's server gate excludes them --
///         Catalog E still wins by the same mechanism. Tiers 0, 1, and 6-12 show no observable divergence
///         between Catalog A and Catalog E, so the race has no visible consequence for them; only tiers 2-5
///         actually disagree.
///     </para>
///     <para>
///         <b>Open question, not resolved by the cited code (carried forward from the contract, not guessed
///         at):</b> whether any individual step of this multi-summon race produces a client-visible effect
///         (e.g. a spawn/despawn animation broadcast) is not established by any citation available to this
///         contract -- the cited code shows only server-side monster-object slot state changes and internal
///         summon-routine calls. This determines whether a faithful reimplementation needs to reproduce the
///         full multi-step race as an observable sequence, or may resolve directly to <see cref="CatalogE" />'s
///         final outcome without an intermediate visible flicker (the choice <see cref="Zone241RebirthTierBossCatalog" />
///         makes, given <see cref="Zone.TryEnterZone241PersonalInstance" />'s existing single-call-site,
///         single-summon architecture) -- flagged for a follow-up check into
///         <c>Server/ts25zone/S10_MySummon.cpp</c>'s instance-summon routine's own broadcast behavior before
///         this is revisited.
///     </para>
/// </remarks>
public static class PersonalDungeonBossTables
{
    /// <summary>
    ///     Catalog A (zone-enter handler, Server/ts25zone/S04_MyWork02.cpp:1167-1183): rebirth tier -&gt; boss
    ///     catalog id. Tiers 0 and 1 both resolve to boss 725. Any value outside 0-12 falls through to the same
    ///     result as tier 12 (boss 750) -- the table's own explicit fallback entry.
    /// </summary>
    public static int ResolveCatalogA(int rebirthTier)
    {
        return rebirthTier switch
        {
            0 or 1 => 725,
            2 => 728,
            3 => 729,
            4 => 730,
            5 => 731,
            6 => 730,
            7 => 736,
            8 => 737,
            9 => 738,
            10 => 748,
            11 => 749,
            _ => 750 // tier 12, and any value outside 0-12
        };
    }

    /// <summary>
    ///     Catalog D (registration-validation routine, servers 325-330 only,
    ///     Server/ts25zone/S07_MyGame03.cpp:6295-6304): legacy server number -&gt; boss catalog id, each
    ///     carrying the legacy display name recorded in the source's own comment. Any value outside 325-330
    ///     falls back to boss 725 -- unreachable in practice, since Catalog D's own precondition (server number
    ///     325-330 and at least one unspent LOD round) means this table is never actually consulted with a
    ///     value outside that range.
    /// </summary>
    public static int ResolveCatalogD(int serverNumber)
    {
        return serverNumber switch
        {
            325 => 725, // "Bai Gu Great"
            326 => 726, // "Pan Guan Judge"
            327 => 727, // "Soul Eater"
            328 => 728, // "Warlord the Great"
            329 => 729, // "Blood Moon Halberd"
            330 => 730, // "Evil Soul Fighter"
            _ => 725 // unreachable in practice -- see remarks
        };
    }

    /// <summary>
    ///     Catalog E (tick-driven summon step, Server/ts25zone/S07_MyGame01.cpp:11819-11861): rebirth tier -&gt;
    ///     boss catalog id. Tiers 0 and 1 both resolve to boss 725. Any value outside 0-12 falls through to the
    ///     same result as tier 12 (boss 750). <b>This is the table that actually determines what a player
    ///     faces</b> -- see class remarks for the confirmed three-way summon race.
    /// </summary>
    public static int ResolveCatalogE(int rebirthTier)
    {
        return rebirthTier switch
        {
            0 or 1 => 725,
            2 => 726,
            3 => 727,
            4 => 728,
            5 => 729,
            6 => 730,
            7 => 736,
            8 => 737,
            9 => 738,
            10 => 748,
            11 => 749,
            _ => 750 // tier 12, and any value outside 0-12
        };
    }

    /// <summary>
    ///     Catalog A's and Catalog E's shared fixed summon position (Server/ts25zone/S04_MyWork02.cpp:1165,
    ///     Server/ts25zone/S07_MyGame01.cpp:11765): X=1, Y=21, Z=0.
    /// </summary>
    public static (float X, float Y, float Z) CatalogAAndESummonPosition => (1f, 21f, 0f);

    /// <summary>
    ///     Catalog D's own fixed summon position (Server/ts25zone/S07_MyGame03.cpp:6289-6292): X=0, Y=21, Z=0 --
    ///     a one-unit difference from <see cref="CatalogAAndESummonPosition" /> on the first axis only. Only
    ///     ever used for the transient monster the race (see class remarks) invalidates moments later.
    /// </summary>
    public static (float X, float Y, float Z) CatalogDSummonPosition => (0f, 21f, 0f);
}
