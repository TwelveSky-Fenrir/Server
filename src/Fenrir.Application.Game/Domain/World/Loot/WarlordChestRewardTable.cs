using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Domain.World.Loot;

public static class WarlordChestRewardTable
{
    public const int SkyChestBoxItemId = 1378;

    public const int EarthChestBoxItemId = 1379;

    public const short MinimumLevel2 = 12;

    public static readonly FrozenDictionary<byte, ImmutableArray<int>> RarePoolsByPreviousTribe =
        new Dictionary<byte, ImmutableArray<int>>
        {
            [0] = [87013, 87014, 87015, 87016, 87017, 87018, 87019, 87020, 87008],
            [1] = [87034, 87035, 87036, 87037, 87038, 87039, 87040, 87041, 87029],
            [2] = [87055, 87056, 87057, 87058, 87059, 87060, 87061, 87062, 87050]
        }.ToFrozenDictionary();

    public static readonly FrozenDictionary<byte, ImmutableArray<int>> ElitePoolsByPreviousTribe =
        new Dictionary<byte, ImmutableArray<int>>
        {
            [0] =
            [
                87071, 87072, 87073, 87074, 87075, 87076,
                87077, 87078, 87079, 87080, 87081, 87082, 87083, 87084
            ],
            [1] =
            [
                87093, 87094, 87095, 87096, 87097, 87098,
                87099, 87100, 87101, 87102, 87103, 87104, 87105, 87106
            ],
            [2] =
            [
                87115, 87116, 87117, 87118, 87119, 87120,
                87121, 87122, 87123, 87124, 87125, 87126, 87127, 87128
            ]
        }.ToFrozenDictionary();

    public static bool MeetsLevelGate(short level2)
    {
        return level2 >= MinimumLevel2;
    }

    public static bool TryRollReward(int boxItemId, byte previousTribe, Random random, out int rewardItemId)
    {
        var pools = boxItemId switch
        {
            EarthChestBoxItemId => RarePoolsByPreviousTribe,
            SkyChestBoxItemId => ElitePoolsByPreviousTribe,
            _ => null
        };

        if (pools is null || !pools.TryGetValue(previousTribe, out var pool) || pool.IsDefaultOrEmpty)
        {
            rewardItemId = 0;
            return false;
        }

        rewardItemId = LootBoxRewardResolver.RollUniform(random, pool);
        return true;
    }
}
