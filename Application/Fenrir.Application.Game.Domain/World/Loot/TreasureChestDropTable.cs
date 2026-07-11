using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.Loot;

/// <summary>Which of the treasure-chest table's five outcomes a roll landed on.</summary>
public enum TreasureChestOutcomeKind
{
    /// <summary>A fixed item id (<see cref="TreasureChestOutcome.ItemId" />).</summary>
    FixedItem,

    /// <summary>
    ///     A uniformly-random level-1 pet index -- <see cref="TreasureChestOutcome.ItemId" /> is 0; the caller
    ///     draws the actual pet from <see cref="TreasureChestDropTable.Level1PetPool" />
    ///     (<c>GetRandomPetL1</c>, <c>Server/ts25zone/S07_MyGame03.cpp:7328-7331</c>).
    /// </summary>
    RandomLevel1Pet
}

/// <summary>One resolved treasure-chest outcome. See <see cref="TreasureChestDropTable" />.</summary>
public readonly record struct TreasureChestOutcome(TreasureChestOutcomeKind Kind, int ItemId)
{
    /// <summary>The 8% "random level-1 pet" outcome -- item id is drawn separately by the caller.</summary>
    public static readonly TreasureChestOutcome RandomPet = new(TreasureChestOutcomeKind.RandomLevel1Pet, 0);

    public static TreasureChestOutcome Item(int itemId)
    {
        return new TreasureChestOutcome(TreasureChestOutcomeKind.FixedItem, itemId);
    }
}

/// <summary>
///     The treasure-chest (<c>MyUtil::ProcessForDropTresureChest</c>) weighted outcome table -- a single 0-99
///     uniform roll selects exactly one of five outcomes with cumulative thresholds. The chosen item is then
///     dropped with quantity 0 and value 0 and reshaped by its own <c>Sort</c> through
///     <see cref="Inventory.InventoryToWorldDropPolicy.ReshapeGroundDrop" /> like any other ground drop.
/// </summary>
/// <remarks>
///     Réf. C++ : <c>Server/ts25zone/S07_MyGame03.cpp:722-753</c> -- the single 0-99 roll and the five weighted
///     outcomes (78% -&gt; 1145, 10% -&gt; 8109, 8% -&gt; random level-1 pet, 3% -&gt; 8110, 1% -&gt; 695). The
///     weights sum to exactly 100, so there is no remainder bucket; the source's former sixth outcome (a
///     rare-elite drop-by-tribe lookup) is commented out and dead, so it is not modeled here.
///     <para>
///         Modeled as a <see cref="FrozenDictionary{TKey,TValue}" /> keyed by the raw 0-99 roll value (built
///         once at static init, read-only for the process lifetime) so <see cref="Resolve" /> is an allocation-free
///         O(1) lookup on the loot path, matching how every other static loot table in this namespace
///         (<see cref="BossDropCatalog" />) is materialized once and never mutated.
///     </para>
///     <para>
///         <b>Unrecovered:</b> the <c>GetRandomPetL1</c> fixed level-1 pet list feeding the 8% outcome was NOT
///         supplied by the C14 contract, so <see cref="Level1PetPool" /> is empty and the pet outcome is returned
///         as <see cref="TreasureChestOutcome.RandomPet" /> (a marker with item id 0) for the caller to resolve
///         once the pool exists. Flagged for cpp-zone-gameplay-analyst.
///     </para>
/// </remarks>
public static class TreasureChestDropTable
{
    /// <summary>78% outcome item id.</summary>
    public const int JackpotItemId = 1145;

    /// <summary>10% outcome item id.</summary>
    public const int SecondItemId = 8109;

    /// <summary>3% outcome item id.</summary>
    public const int FourthItemId = 8110;

    /// <summary>1% outcome item id.</summary>
    public const int RareItemId = 695;

    /// <summary>Inclusive lower/upper bound of the single roll (0-99).</summary>
    public const int RollExclusiveUpperBound = 100;

    private static readonly FrozenDictionary<int, TreasureChestOutcome> OutcomeByRoll = BuildTable();

    /// <summary>
    ///     <c>GetRandomPetL1</c>'s fixed level-1 pet list (the 8% outcome). Empty -- its contents were not
    ///     supplied by the C14 contract; see this type's remarks.
    /// </summary>
    public static ImmutableArray<int> Level1PetPool { get; } = [];

    /// <summary>Maps a single 0-99 roll to its outcome.</summary>
    public static TreasureChestOutcome Resolve(int roll)
    {
        if (roll is < 0 or >= RollExclusiveUpperBound)
            throw new ArgumentOutOfRangeException(nameof(roll), roll,
                $"Treasure-chest roll must be 0-{RollExclusiveUpperBound - 1}.");

        return OutcomeByRoll[roll];
    }

    /// <summary>Draws one 0-99 roll (plain uniform, NOT the skewed <see cref="LootRandomSource" />) and resolves it.</summary>
    public static TreasureChestOutcome Roll(Random random)
    {
        return Resolve(random.Next(RollExclusiveUpperBound));
    }

    private static FrozenDictionary<int, TreasureChestOutcome> BuildTable()
    {
        var map = new Dictionary<int, TreasureChestOutcome>(RollExclusiveUpperBound);
        for (var roll = 0; roll < RollExclusiveUpperBound; roll++)
            map[roll] = roll switch
            {
                < 78 => TreasureChestOutcome.Item(JackpotItemId), // 0-77  : 78%
                < 88 => TreasureChestOutcome.Item(SecondItemId), //  78-87 : 10%
                < 96 => TreasureChestOutcome.RandomPet, //           88-95 :  8%
                < 99 => TreasureChestOutcome.Item(FourthItemId), //  96-98 :  3%
                _ => TreasureChestOutcome.Item(RareItemId) //        99    :  1%
            };

        return map.ToFrozenDictionary();
    }
}
