using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.Loot;

public enum TreasureChestOutcomeKind
{

        FixedItem,

        RandomLevel1Pet
}

public readonly record struct TreasureChestOutcome(TreasureChestOutcomeKind Kind, int ItemId)
{

        public static readonly TreasureChestOutcome RandomPet = new(TreasureChestOutcomeKind.RandomLevel1Pet, 0);

    public static TreasureChestOutcome Item(int itemId)
    {
        return new TreasureChestOutcome(TreasureChestOutcomeKind.FixedItem, itemId);
    }
}

public static class TreasureChestDropTable
{

        public const int JackpotItemId = 1145;

        public const int SecondItemId = 8109;

        public const int FourthItemId = 8110;

        public const int RareItemId = 695;

        public const int RollExclusiveUpperBound = 100;

    private static readonly FrozenDictionary<int, TreasureChestOutcome> OutcomeByRoll = BuildTable();

        public static ImmutableArray<int> Level1PetPool { get; } = [];

        public static TreasureChestOutcome Resolve(int roll)
    {
        if (roll is < 0 or >= RollExclusiveUpperBound)
            throw new ArgumentOutOfRangeException(nameof(roll), roll,
                $"Treasure-chest roll must be 0-{RollExclusiveUpperBound - 1}.");

        return OutcomeByRoll[roll];
    }

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
                < 78 => TreasureChestOutcome.Item(JackpotItemId),
                < 88 => TreasureChestOutcome.Item(SecondItemId),
                < 96 => TreasureChestOutcome.RandomPet,
                < 99 => TreasureChestOutcome.Item(FourthItemId),
                _ => TreasureChestOutcome.Item(RareItemId)
            };

        return map.ToFrozenDictionary();
    }
}
