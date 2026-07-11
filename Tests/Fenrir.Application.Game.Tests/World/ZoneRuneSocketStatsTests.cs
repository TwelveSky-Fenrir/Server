using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Pets;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World;

public class ZoneRuneSocketStatsTests
{
    private static PlayerRuntimeState EnterPlayer(Zone zone, int characterId = 10, short level = 42)
    {
        var (session, _) = ZoneTestKit.CreateSession(characterId);
        zone.Post(ZoneCommand.Enter(characterId, ZoneTestKit.EnterData(session, 1, level: level)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(zone.TryGetPlayer(characterId, out var state));
        return state!;
    }

        private static EffectiveStats RecomputeExpected(PlayerRuntimeState state)
    {
        var attributes = new CharacterBaseAttributes(state.StatVit, state.StatStr, state.StatInt, state.StatDex,
            state.Level, state.Tribe, state.PreviousTribe, state.Title, state.Halo, state.RebirthCount,
            state.Level2);
        var worldData = ZoneTestKit.EmptyWorldData();
        var equipmentContainer = state.Inventory.GetContainer(ContainerMatrix.Equipment);
        var petItemId = equipmentContainer.TryGetValue(PetSlots.EquipmentSlot, out var petStack)
            ? petStack.ItemId
            : 0;
        var petContribution = PetGrowthCalculator.Compute(petItemId, state.PetGrowth, state.PetActivity,
            worldData.ItemsById);

        return EquipmentService.RecomputeStats(attributes, equipmentContainer, worldData, state.Buffs,
            petContribution, runtimeState: state);
    }

    [Fact]
    public void Insert_NoUpdatedStatsOverride_RecomputesStatsWithLiveRuneContribution()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var state = EnterPlayer(zone);

        Assert.Null(state.Stats);
        var beforeExpected = RecomputeExpected(state);

        var packedStat = ItemValueCodec.Encode(0, 0, 60, 0);
        zone.PostRuneSocketCommand(new RuneSocketZoneCommand(state.CharacterId, 2, 93516, packedStat, null));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(93516, state.RuneSystem[2]);
        Assert.Equal(packedStat, state.RuneSystemStat[2]);
        Assert.NotNull(state.Stats);

        var afterExpected = RecomputeExpected(state);

        Assert.NotEqual(beforeExpected.MaxLife, afterExpected.MaxLife);
        Assert.Equal(afterExpected, state.Stats!.Value);

        Assert.Equal(beforeExpected.MaxLife + 1200, afterExpected.MaxLife);
        Assert.Equal(beforeExpected.MaxMana, afterExpected.MaxMana);
        Assert.Equal(beforeExpected.AttackPower, afterExpected.AttackPower);
    }

    [Fact]
    public void Remove_ClearsRuneStatContributionOnNextRecompute()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var state = EnterPlayer(zone);

        var packedStat = ItemValueCodec.Encode(0, 0, 60, 0);
        zone.PostRuneSocketCommand(new RuneSocketZoneCommand(state.CharacterId, 2, 93516, packedStat, null));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        var withRune = state.Stats!.Value;

        zone.PostRuneSocketCommand(new RuneSocketZoneCommand(state.CharacterId, 2, null, null, null));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(0, state.RuneSystem[2]);
        Assert.Equal(0, state.RuneSystemStat[2]);
        Assert.Equal(withRune.MaxLife - 1200, state.Stats!.Value.MaxLife);
    }

    [Fact]
    public void ExplicitUpdatedStatsOverride_StillWinsLastOverTheRecompute()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var state = EnterPlayer(zone);

        var overrideStats = new EffectiveStats(999999, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10);

        zone.PostRuneSocketCommand(new RuneSocketZoneCommand(state.CharacterId, 0, 93514,
            ItemValueCodec.Encode(0, 0, 0, 0), overrideStats));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.Equal(overrideStats, state.Stats);
    }
}
