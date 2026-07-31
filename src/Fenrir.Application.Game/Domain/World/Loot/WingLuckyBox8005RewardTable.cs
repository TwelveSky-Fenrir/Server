using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Domain.World.Loot;

public static class WingLuckyBox8005RewardTable
{
    public const int BoxId = 8005;

    public const int BlueDragonWingsThresholdExclusive = 80;

    public const int ArchangelWingsThresholdExclusive = 130;

    public const int FixedRewardId2477 = 2477;

    public const int FixedRewardCeilingInclusive = 5;

    public const int TribeSecondCeilingInclusive = 60;

    public const int MiscPoolCeilingInclusive = 100;

    public const int ElixirPoolCeilingInclusive = 160;

    public static readonly FrozenDictionary<byte, int> BlueDragonWingsIdByPreviousTribe =
        new Dictionary<byte, int> { [0] = 213, [1] = 214, [2] = 215 }.ToFrozenDictionary();

    public static readonly FrozenDictionary<byte, int> ArchangelWingsIdByPreviousTribe =
        new Dictionary<byte, int> { [0] = 216, [1] = 217, [2] = 218 }.ToFrozenDictionary();

    public static readonly FrozenDictionary<byte, int> TribeSecondIdByPreviousTribe =
        new Dictionary<byte, int> { [0] = 201, [1] = 202, [2] = 203 }.ToFrozenDictionary();

    public static readonly ImmutableArray<int> MiscPoolIds = [2397, 694, 693, 692, 696, 698];

    public static readonly ImmutableArray<int> ElixirPoolIds = [506, 507, 508, 509, 578, 579];

    public static readonly ImmutableArray<int> CharmPoolIds = [1166, 1118, 1103, 1222, 1145, 1237];

    public static RollResult Roll(byte previousTribe, Random random)
    {
        var first = random.Next(0, 10_000);

        if (first < BlueDragonWingsThresholdExclusive)
            return BlueDragonWingsIdByPreviousTribe.TryGetValue(previousTribe, out var blueDragonId)
                ? new RollResult(true, blueDragonId)
                : RollResult.Failure;

        if (first < ArchangelWingsThresholdExclusive)
            return ArchangelWingsIdByPreviousTribe.TryGetValue(previousTribe, out var archangelId)
                ? new RollResult(true, archangelId)
                : RollResult.Failure;

        var second = random.Next(0, 200);

        if (second <= FixedRewardCeilingInclusive)
            return new RollResult(true, FixedRewardId2477);

        if (second <= TribeSecondCeilingInclusive)
            return TribeSecondIdByPreviousTribe.TryGetValue(previousTribe, out var tribeSecondId)
                ? new RollResult(true, tribeSecondId)
                : RollResult.Failure;

        if (second <= MiscPoolCeilingInclusive)
            return new RollResult(true, LootBoxRewardResolver.RollUniform(random, MiscPoolIds));

        if (second <= ElixirPoolCeilingInclusive)
            return new RollResult(true, LootBoxRewardResolver.RollUniform(random, ElixirPoolIds));

        return new RollResult(true, LootBoxRewardResolver.RollUniform(random, CharmPoolIds));
    }

    public readonly record struct RollResult(bool Success, int RewardItemId)
    {
        public static RollResult Failure { get; } = new(false, 0);
    }
}
