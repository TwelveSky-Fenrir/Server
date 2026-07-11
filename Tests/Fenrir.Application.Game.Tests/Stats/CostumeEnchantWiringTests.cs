using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Stats;

public class CostumeEnchantWiringTests
{
    private static CharacterBaseAttributes NoBonusAttributes()
    {
        return new CharacterBaseAttributes(0, 0, 0, 0, 1, 0, 0, 0, 0, 0);
    }

    private static PlayerRuntimeState State(int costumeIndex, ImmutableArray<int> costumeDate)
    {
        return new PlayerRuntimeState
        {
            CharacterId = 1,
            Session = ZoneTestKit.CreateSession(1).Session,
            Name = "Hero",
            Tribe = 0,
            Gender = 0,
            HeadType = 0,
            FaceType = 0,
            Level = 1,
            CostumeIndex = costumeIndex,
            CostumeDate = costumeDate
        };
    }

    [Fact]
    public void CostumeDate_DefaultsToTenZeroSlots()
    {
        var state = new PlayerRuntimeState
        {
            CharacterId = 1,
            Session = ZoneTestKit.CreateSession(1).Session,
            Name = "Hero",
            Tribe = 0,
            Gender = 0,
            HeadType = 0,
            FaceType = 0,
            Level = 1
        };

        Assert.Equal(10, state.CostumeDate.Length);
        Assert.All(state.CostumeDate, v => Assert.Equal(0, v));

        Assert.Equal(-1, state.CostumeIndex);
    }

    [Fact]
    public void RecomputeStats_DefaultCostumeIndex_IgnoresPopulatedCostumeDate()
    {
        var attributes = NoBonusAttributes();
        var worldData = ZoneTestKit.EmptyWorldData();
        var equipment = ImmutableDictionary<byte, ItemStack>.Empty;
        ImmutableArray<int> populatedDate = [11, 22, 33, 44, 55, 66, 77, 88, 99, 111];

        var withoutState = EquipmentService.RecomputeStats(attributes, equipment, worldData);
        var withUnwornCostume =
            EquipmentService.RecomputeStats(attributes, equipment, worldData, runtimeState: State(-1, populatedDate));

        Assert.Equal(withoutState, withUnwornCostume);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(20)]
    public void RecomputeStats_IndexOutsideWornRange_ContributesNothing(int costumeIndex)
    {
        var attributes = NoBonusAttributes();
        var worldData = ZoneTestKit.EmptyWorldData();
        var equipment = ImmutableDictionary<byte, ItemStack>.Empty;
        ImmutableArray<int> populatedDate = [50, 50, 50, 50, 50, 50, 50, 50, 50, 50];

        var withoutState = EquipmentService.RecomputeStats(attributes, equipment, worldData);
        var withState = EquipmentService.RecomputeStats(attributes, equipment, worldData,
            runtimeState: State(costumeIndex, populatedDate));

        Assert.Equal(withoutState, withState);
    }

    [Fact]
    public void RecomputeStats_WornCostumeIndex_DecodesSlotAndAddsCriticalAndLuck()
    {
        var attributes = NoBonusAttributes();
        var worldData = ZoneTestKit.EmptyWorldData();
        var equipment = ImmutableDictionary<byte, ItemStack>.Empty;
        ImmutableArray<int> date = [50, 0, 0, 0, 0, 0, 0, 0, 0, 0];

        var baseline = EquipmentService.RecomputeStats(attributes, equipment, worldData);
        var withWornCostume =
            EquipmentService.RecomputeStats(attributes, equipment, worldData, runtimeState: State(10, date));

        Assert.Equal(baseline.Critical + 5, withWornCostume.Critical);
        Assert.Equal(baseline.Luck + 100, withWornCostume.Luck);

        Assert.Equal(baseline.MaxMana, withWornCostume.MaxMana);
        Assert.Equal(baseline.AttackPower, withWornCostume.AttackPower);
        Assert.Equal(baseline.DefensePower, withWornCostume.DefensePower);
        Assert.Equal(baseline.CriticalDefence, withWornCostume.CriticalDefence);
    }

    [Fact]
    public void RecomputeStats_WornCostumeIndex_ReadsSignedLowByteNegative()
    {
        var attributes = NoBonusAttributes();
        var worldData = ZoneTestKit.EmptyWorldData();
        var equipment = ImmutableDictionary<byte, ItemStack>.Empty;
        ImmutableArray<int> date = [0, 0, 0, 0, 0, 0, 0, 0, 0, 200];

        var baseline = EquipmentService.RecomputeStats(attributes, equipment, worldData);
        var withWornCostume =
            EquipmentService.RecomputeStats(attributes, equipment, worldData, runtimeState: State(19, date));

        Assert.Equal(baseline.Critical, withWornCostume.Critical);
        Assert.Equal(baseline.Luck - 112, withWornCostume.Luck);
    }
}
