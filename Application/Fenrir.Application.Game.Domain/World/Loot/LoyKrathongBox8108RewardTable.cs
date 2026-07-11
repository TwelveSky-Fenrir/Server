using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Domain.World.Loot;

public static class LoyKrathongBox8108RewardTable
{

        public const int BoxId = 8108;

        public const int FixedRewardId1407 = 1407;

        public const int FixedBandThresholdExclusive = 6;

        public const int SeventhsBandThresholdExclusive = 8;

        public const int SeventhsMajorityId = 1403;

        public const int SeventhsMinorityId = 1404;

        public const int SeventhsDenominator = 7;

        public const int SeventhsMajorityNumerator = 4;

        public const int EpicBandThresholdExclusive = 10;

        public static readonly FrozenDictionary<byte, ImmutableArray<int>> EpicIdsByPreviousTribe =
        new Dictionary<byte, ImmutableArray<int>>
        {
            [0] = [90787, 90786, 90788],
            [1] = [90789, 90790, 90791],
            [2] = [90793, 90792, 90794]
        }.ToFrozenDictionary();

        public const int SixthsBandThresholdExclusive = 12;

        public const int SixthsMinorityId = 826;

        public const int SixthsMajorityId = 619;

        public const int SixthsDenominator = 6;

        public const int SixthsMinorityNumerator = 2;

        public static readonly ImmutableArray<int> CommonPoolIds =
        [1103, 1237, 1166, 578, 579, 1017, 1018, 1092, 1093, 698, 696, 695];

        public static RollResult Roll(byte previousTribe, Random random)
    {
        var roll = random.Next(0, 100);

        if (roll < FixedBandThresholdExclusive)
            return new RollResult(true, FixedRewardId1407);

        if (roll < SeventhsBandThresholdExclusive)
        {
            var sub = random.Next(0, SeventhsDenominator);
            return new RollResult(true, sub < SeventhsMajorityNumerator ? SeventhsMajorityId : SeventhsMinorityId);
        }

        if (roll < EpicBandThresholdExclusive)
            return EpicIdsByPreviousTribe.TryGetValue(previousTribe, out var epicPool)
                ? new RollResult(true, LootBoxRewardResolver.RollUniform(random, epicPool))
                : RollResult.Failure;

        if (roll < SixthsBandThresholdExclusive)
        {
            var sub = random.Next(0, SixthsDenominator);
            return new RollResult(true, sub < SixthsMinorityNumerator ? SixthsMinorityId : SixthsMajorityId);
        }

        return new RollResult(true, LootBoxRewardResolver.RollUniform(random, CommonPoolIds));
    }

        public readonly record struct RollResult(bool Success, int RewardItemId)
    {
        public static RollResult Failure { get; } = new(false, 0);
    }
}
