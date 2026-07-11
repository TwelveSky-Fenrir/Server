using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.World.Loot;

public static class CostumeChest76543RewardTable
{

        public const int BoxItemId = 76543;

        public const int RentalDays = 3;

        public const int EnchantGradeMin = 30;

        public const int EnchantGradeMax = 60;

        public static readonly FrozenDictionary<byte, int> RewardIdByPreviousTribe =
        new Dictionary<byte, int> { [0] = 76524, [1] = 76525, [2] = 76526 }.ToFrozenDictionary();

        public static bool TryResolveRewardId(byte previousTribe, out int rewardItemId)
    {
        return RewardIdByPreviousTribe.TryGetValue(previousTribe, out rewardItemId);
    }

        public static int RollEnchantGrade(Random random)
    {
        return random.Next(EnchantGradeMin, EnchantGradeMax + 1);
    }

        public static RollResult Roll(byte previousTribe, Random random)
    {
        if (!TryResolveRewardId(previousTribe, out var rewardItemId))
            return RollResult.Failure;

        return new RollResult(true, rewardItemId, RollEnchantGrade(random));
    }

        public readonly record struct RollResult(bool Success, int RewardItemId, int EnchantGrade)
    {
        public static RollResult Failure { get; } = new(false, 0, 0);
    }
}
