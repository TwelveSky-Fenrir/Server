namespace Fenrir.Application.Game.Domain.World.Loot;

/// <summary>
///     The two "random animal" and one "random elixir" item pickers a couple of the
///     <see cref="BossEventDropResolver" /> blocks resolve at drop time (identifier 1408's low/low-mid tiers and
///     the 746/9001 shared pool's elixir slot). Modeled as a per-kill helper invocation, NOT baked into a static
///     boss pool, because the chosen animal/elixir is a fresh uniform draw on every kill -- see
///     <see cref="BossEventDropResolver" />'s own remarks.
/// </summary>
/// <remarks>
///     Réf. C++ : the animal picker reads an eight-row table (<c>Server/ts25zone/S07_MyGame03.cpp:7342-7359</c>) --
///     the tier-1 picker reads that table's column 0, the tier-2 picker its column 1 -- and the elixir picker a
///     six-entry set (<c>Server/ts25zone/S07_MyGame03.cpp:7308-7314</c>), both drawn uniformly with the
///     game-specific generator. Fenrir already collapses the legacy's two distinct RNG streams (game-specific vs.
///     the platform <c>rand()</c> the 1408 tier roll uses) onto the one <see cref="Random" /> the whole
///     <c>ProcessDeath</c> path threads, so these picks share that stream too -- same modeling decision
///     <see cref="BossEventDropResolver" />'s already-verified control flow made, not a new one.
///     <para>
///         Two sibling tables the legacy source also defines but these boss blocks never touch are deliberately
///         omitted: the ninth animal family (ids in the 1329-1331 band) and tier-3 animal id 1328 are absent from
///         the eight-row table, and the "no mana potion" / "plus" elixir variant sets are not the set these blocks
///         read -- see the contract's own "Helper resolution" edge cases.
///     </para>
/// </remarks>
public static class BossDropHelperResolver
{
    /// <summary>
    ///     Tier-1 animal set (column 0 of the eight-row table): drawn by identifier 1408's low-mid tier
    ///     (<c>Server/ts25zone/S07_MyGame05.cpp:2532-2583</c>).
    /// </summary>
    private static readonly int[] Tier1Animals = [1301, 1302, 1303, 1313, 1317, 1320, 1323, 1326];

    /// <summary>
    ///     Tier-2 animal set (column 1 of the eight-row table): drawn by identifier 1408's low tier
    ///     (<c>Server/ts25zone/S07_MyGame05.cpp:2532-2583</c>).
    /// </summary>
    private static readonly int[] Tier2Animals = [1304, 1305, 1306, 1314, 1318, 1321, 1324, 1327];

    /// <summary>
    ///     Six-entry elixir set drawn by the 746/9001 shared pool's fourth slot
    ///     (<c>Server/ts25zone/S07_MyGame05.cpp:2586-2662</c>).
    /// </summary>
    private static readonly int[] Elixirs = [506, 507, 508, 509, 578, 579];

    /// <summary>One uniformly-drawn tier-1 animal id.</summary>
    public static int ResolveRandomTier1Animal(Random random)
    {
        return Tier1Animals[random.Next(Tier1Animals.Length)];
    }

    /// <summary>One uniformly-drawn tier-2 animal id.</summary>
    public static int ResolveRandomTier2Animal(Random random)
    {
        return Tier2Animals[random.Next(Tier2Animals.Length)];
    }

    /// <summary>One uniformly-drawn elixir id.</summary>
    public static int ResolveRandomElixir(Random random)
    {
        return Elixirs[random.Next(Elixirs.Length)];
    }
}
