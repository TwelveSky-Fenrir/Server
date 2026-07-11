using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Domain.World.Loot;

public static class ChestBox720RewardTable
{
    public const int BoxId = 720;

    public const int TribePoolThresholdExclusive = 15;

    public const int FixedRewardIdSlot2 = 1449;

    public const int FixedRewardIdSlot3 = 1072;

    public const int FixedRewardIdSlot5 = 1437;

    public const int FixedRewardIdSlot6 = 1178;

    public const int FixedRewardIdSlot7 = 698;

    public const int FixedRewardIdSlot8 = 1166;

    public static readonly ImmutableArray<int> TribePoolBaseIds =
        [15157, 15267, 15135, 15179, 15223, 15245, 15289];

    public static readonly FrozenDictionary<byte, int> TribeOffsetByPreviousTribe =
        new Dictionary<byte, int> { [0] = 0, [1] = 20000, [2] = 40000 }.ToFrozenDictionary();


    public static readonly ImmutableArray<int> AnimalPoolIds = [1301, 1302, 1303, 1313, 1317, 1320, 1323, 1326];

    public static readonly ImmutableArray<int> ElixirPlusPoolIds = [801, 802, 803, 804, 805, 806];

    public static RollResult Roll(byte previousTribe, Random random)
    {
        var outer = random.Next(0, 100);

        if (outer < TribePoolThresholdExclusive)
        {
            if (!TribeOffsetByPreviousTribe.TryGetValue(previousTribe, out var offset))
                return RollResult.Failure;

            var baseId = TribePoolBaseIds[random.Next(0, TribePoolBaseIds.Length)];
            return new RollResult(true, baseId + offset);
        }

        var slot = random.Next(0, 8);
        return slot switch
        {
            0 => new RollResult(true, LootBoxRewardResolver.RollUniform(random, AnimalPoolIds)),
            1 => new RollResult(true, FixedRewardIdSlot2),
            2 => new RollResult(true, FixedRewardIdSlot3),
            3 => new RollResult(true, LootBoxRewardResolver.RollUniform(random, ElixirPlusPoolIds)),
            4 => new RollResult(true, FixedRewardIdSlot5),
            5 => new RollResult(true, FixedRewardIdSlot6),
            6 => new RollResult(true, FixedRewardIdSlot7),
            _ => new RollResult(true, FixedRewardIdSlot8)
        };
    }

    public readonly record struct RollResult(bool Success, int RewardItemId)
    {
        public static RollResult Failure { get; } = new(false, 0);
    }
}
