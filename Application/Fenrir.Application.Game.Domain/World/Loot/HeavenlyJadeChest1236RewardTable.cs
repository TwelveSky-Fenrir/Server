using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Domain.World.Loot;

public static class HeavenlyJadeChest1236RewardTable
{

        public const int BoxId = 1236;

        public static readonly FrozenDictionary<byte, int> CatHairBandIdByPreviousTribe =
        new Dictionary<byte, int> { [0] = 2307, [1] = 2308, [2] = 2309 }.ToFrozenDictionary();

        public const int ZeroBranchAlternateId = 1321;

        public const int ZeroBranchFallbackId = 1324;

        public static readonly ImmutableArray<int> OneToThirtyPair = [1007, 1008];

        public static readonly FrozenDictionary<byte, int> TribeFixedIdByPreviousTribe =
        new Dictionary<byte, int> { [0] = 126, [1] = 129, [2] = 132 }.ToFrozenDictionary();

        public static readonly ImmutableArray<int> FiftyOneToHundredTriple = [601, 602, 2249];

        public static readonly ImmutableArray<int> ElixirNoMPPoolIds = [506, 508, 509, 578, 579];

        public const int SevenHundredBranchId = 1045;

        public static RollResult Roll(byte previousTribe, Random random)
    {
        var outer = random.Next(0, 1000);

        if (outer == 0)
        {
            var inner = random.Next(0, 10);
            if (inner == 0)
                return CatHairBandIdByPreviousTribe.TryGetValue(previousTribe, out var catHairBandId)
                    ? new RollResult(true, catHairBandId)
                    : RollResult.Failure;

            return inner == 1
                ? new RollResult(true, ZeroBranchAlternateId)
                : new RollResult(true, ZeroBranchFallbackId);
        }

        if (outer <= 30)
        {
            var coin = random.Next(0, 2);
            return new RollResult(true, OneToThirtyPair[coin]);
        }

        if (outer <= 50)
            return TribeFixedIdByPreviousTribe.TryGetValue(previousTribe, out var tribeFixedId)
                ? new RollResult(true, tribeFixedId)
                : RollResult.Failure;

        if (outer <= 100)
        {
            var third = random.Next(0, 3);
            return new RollResult(true, FiftyOneToHundredTriple[third]);
        }

        if (outer <= 699)
            return new RollResult(true, LootBoxRewardResolver.RollUniform(random, ElixirNoMPPoolIds));

        return new RollResult(true, SevenHundredBranchId);
    }

        public readonly record struct RollResult(bool Success, int RewardItemId)
    {
        public static RollResult Failure { get; } = new(false, 0);
    }
}
