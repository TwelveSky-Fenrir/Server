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

            BoxRewardSpec.Weighted(1043, [
                new LootBoxRewardResolver.WeightedReward(699, 1),
                new LootBoxRewardResolver.WeightedReward(1437, 1),
                new LootBoxRewardResolver.WeightedReward(576, 8),
                new LootBoxRewardResolver.WeightedReward(1023, 90),
                new LootBoxRewardResolver.WeightedReward(1022, 100),
                new LootBoxRewardResolver.WeightedReward(1021, 100),
                new LootBoxRewardResolver.WeightedReward(1020, 100),
                new LootBoxRewardResolver.WeightedReward(1019, 200)
            ]),

            BoxRewardSpec.Uniform(76544, [8001, 8002, 8003, 8105, 8110, 1126, 8405, 8407, 8408])
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

    public static FrozenSet<int> EliteOnlyNoticeBoxIds { get; } = new[] { 1035, 1036, 1037 }.ToFrozenSet();

    public static FrozenSet<int> NoticeRewardWhitelist { get; } = new[] { 1012, 1016 }.ToFrozenSet();

    public BoxRewardSpec? TryGetSpec(int boxId)
    {
        return _byBoxId.TryGetValue(boxId, out var spec) ? spec : null;
    }
}
