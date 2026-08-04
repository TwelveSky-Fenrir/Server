using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Domain.World.Loot;

public sealed class LootBoxCatalog
{
    private readonly FrozenDictionary<int, BoxRewardSpec> _byBoxId;

    private LootBoxCatalog()
    {
        var specs = new List<BoxRewardSpec>
        {
            BoxRewardSpec.RareBandThenPools(601,
                ImmutableArray<LootBoxRewardResolver.RewardBand>.Empty,
                [
                    new LootBoxRewardResolver.RewardPool(10, [92286]),
                    new LootBoxRewardResolver.RewardPool(40, [1301, 1302, 1303, 1313, 1317, 1320, 1323, 1326]),
                    new LootBoxRewardResolver.RewardPool(100, [611, 612, 652]),
                    new LootBoxRewardResolver.RewardPool(160, [506, 507, 508, 509, 578, 579]),
                    new LootBoxRewardResolver.RewardPool(199, [1166, 1118, 1103, 1222, 1145, 1237])
                ]),

            PetBoxRewardTable.Spec,

            MountBox635RewardTable.CreateSpec(),

            CloakBoxRewardTable.Spec,

            BoxRewardSpec.Weighted(7105,
            [
                new LootBoxRewardResolver.WeightedReward(696, 73),
                new LootBoxRewardResolver.WeightedReward(698, 20),
                new LootBoxRewardResolver.WeightedReward(2397, 7)
            ]),

            StellarBox8112RewardTable.Spec,

            BoxRewardSpec.Uniform(76542, [76534, 76535, 76536, 76537, 76538], 3),

            OverEnchantBox8113RewardTable.Spec,

            PillLuckyBag1240RewardTable.Spec,

            M15PetLuckyBox8111RewardTable.Spec,

            CloakVariantBox8114RewardTable.Spec,

            MountVariantBox8115RewardTable.Spec,

            BoxRewardSpec.Uniform(800, [801, 802, 803, 804, 805, 806]),

            BoxRewardSpec.Uniform(76544, [8001, 8002, 8003, 8105, 8110, 1126, 8405, 8407, 8408]),

            BoxRewardSpec.Weighted(582,
            [
                new LootBoxRewardResolver.WeightedReward(506, 1),
                new LootBoxRewardResolver.WeightedReward(507, 1),
                new LootBoxRewardResolver.WeightedReward(508, 1),
                new LootBoxRewardResolver.WeightedReward(509, 1),
                new LootBoxRewardResolver.WeightedReward(578, 1),
                new LootBoxRewardResolver.WeightedReward(579, 1),
                new LootBoxRewardResolver.WeightedReward(1018, 12)
            ]),

            BoxRewardSpec.RareBandThenPools(583, ImmutableArray<LootBoxRewardResolver.RewardBand>.Empty,
            [
                new LootBoxRewardResolver.RewardPool(10, [92286]),
                new LootBoxRewardResolver.RewardPool(40, [1301, 1302, 1303, 1313, 1317, 1320, 1323, 1326]),
                new LootBoxRewardResolver.RewardPool(100, [611, 612, 652]),
                new LootBoxRewardResolver.RewardPool(160, [506, 507, 508, 509, 578, 579]),
                new LootBoxRewardResolver.RewardPool(199, [1166, 1118, 1103, 1222, 1145, 1237])
            ]),

            BoxRewardSpec.RareBandThenPools(584, ImmutableArray<LootBoxRewardResolver.RewardBand>.Empty,
                PetBoxRewardTable.Pools),

            BoxRewardSpec.Weighted(586,
            [
                new LootBoxRewardResolver.WeightedReward(1041, 4),
                new LootBoxRewardResolver.WeightedReward(1037, 7),
                new LootBoxRewardResolver.WeightedReward(1036, 10),
                new LootBoxRewardResolver.WeightedReward(1035, 79)
            ])
        };

        _byBoxId = specs.ToFrozenDictionary(spec => spec.BoxId);
        RegisteredBoxIds = specs.Select(spec => spec.BoxId).ToImmutableArray();
    }

    public static LootBoxCatalog Default { get; } = new();

    public ImmutableArray<int> RegisteredBoxIds { get; }

    public static FrozenSet<int> BulkOpenWhitelist { get; } = new[]
    {
        512, 601, 602, 8112, 8113, 664, 720, 1236, 1240, 2249, 7105, 8108, 8111, 76543, 76544, 8005
    }.ToFrozenSet();

    public static FrozenSet<int> NoticeRewardWhitelist { get; } = new[]
    {
        1012, 1013, 1014, 1015, 1016,
        1304, 1305, 1306, 1307, 1308, 1309,
        1314, 1315, 1318, 1319, 1321, 1322, 1324, 1325, 1327, 1328,
        2208, 2218, 2228, 2238,
        90786, 90787, 90788, 90789, 90790, 90791, 90792, 90793, 90794
    }.ToFrozenSet();

    public BoxRewardSpec? TryGetSpec(int boxId)
    {
        return _byBoxId.TryGetValue(boxId, out var spec) ? spec : null;
    }
}
