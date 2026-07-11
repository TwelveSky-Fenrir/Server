using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class TribeSymbolDamageUpBonusTests
{
    private const short MapId = 1;

    private static (WorldStateService WorldState, TribeSymbolCombatModifiers Modifiers,
        TribeSymbolDamageModifierSystem System) SetUp()
    {
        var worldState = ZoneTestKit.CreateWorldState();
        var modifiers = new TribeSymbolCombatModifiers();
        var system = new TribeSymbolDamageModifierSystem(worldState, modifiers);
        return (worldState, modifiers, system);
    }

    [Fact]
    public void FreshWorldState_EveryTribeOwnsOnlyItsOwnSlot_NoPointsYet_EveryTribeGetsZero()
    {
        var (_, modifiers, system) = SetUp();
        var zone = ZoneTestKit.CreateZone(MapId);

        system.Simulate(zone, 1);

        for (byte tribeId = 0; tribeId < WorldStateService.TribeCount; tribeId++)
            Assert.Equal(0, modifiers.GetDamageUpBonusIncrementCount(tribeId));
    }

    [Fact]
    public void TribeDoesNotOwnItsOwnSlot_GateFails_ZeroRegardlessOfAnythingElse()
    {
        var (worldState, modifiers, system) = SetUp();
        worldState.ResolveTribeSymbol(0, 1);
        worldState.AddTribePoints(0, 100);
        var zone = ZoneTestKit.CreateZone(MapId);

        system.Simulate(zone, 1);

        Assert.Equal(0, modifiers.GetDamageUpBonusIncrementCount(0));
    }

    [Fact]
    public void OwnsOneOtherSlot_OneIncrement()
    {
        var (worldState, modifiers, system) = SetUp();
        worldState.ResolveTribeSymbol(1, 0);
        var zone = ZoneTestKit.CreateZone(MapId);

        system.Simulate(zone, 1);

        Assert.Equal(1, modifiers.GetDamageUpBonusIncrementCount(0));
    }

    [Fact]
    public void OwnsAllThreeOtherSlots_ThreeIncrements()
    {
        var (worldState, modifiers, system) = SetUp();
        worldState.ResolveTribeSymbol(1, 0);
        worldState.ResolveTribeSymbol(2, 0);
        worldState.ResolveTribeSymbol(3, 0);
        var zone = ZoneTestKit.CreateZone(MapId);

        system.Simulate(zone, 1);

        Assert.Equal(3, modifiers.GetDamageUpBonusIncrementCount(0));
    }

    [Fact]
    public void OwnsAllThreeOtherSlotsAndTheMonsterSymbol_FourIncrements_TheMaximum()
    {
        var (worldState, modifiers, system) = SetUp();
        worldState.ResolveTribeSymbol(1, 0);
        worldState.ResolveTribeSymbol(2, 0);
        worldState.ResolveTribeSymbol(3, 0);
        worldState.ResolveMonsterSymbol(0);
        var zone = ZoneTestKit.CreateZone(MapId);

        system.Simulate(zone, 1);

        Assert.Equal(TribeSymbolCombatModifiers.MaxDamageUpBonusIncrementCount,
            modifiers.GetDamageUpBonusIncrementCount(0));
    }

    [Fact]
    public void MonsterSymbolOwnedByAnUnrelatedTribe_DoesNotCountForThisTribe()
    {
        var (worldState, modifiers, system) = SetUp();
        worldState.ResolveMonsterSymbol(1);
        var zone = ZoneTestKit.CreateZone(MapId);

        system.Simulate(zone, 1);

        Assert.Equal(0, modifiers.GetDamageUpBonusIncrementCount(0));
    }

    [Fact]
    public void AllySlotControlCountsTowardTheOwnersBonus_ButNotTheAllysOwnUnrelatedGate()
    {
        var (worldState, modifiers, system) = SetUp();
        worldState.SetAllianceOffer(0, 1, true);
        worldState.ResolveTribeSymbol(2, 1);
        var zone = ZoneTestKit.CreateZone(MapId);

        system.Simulate(zone, 1);

        Assert.Equal(1, modifiers.GetDamageUpBonusIncrementCount(0));
        Assert.Equal(1, modifiers.GetDamageUpBonusIncrementCount(1));
    }

    [Fact]
    public void SmallTribeFallback_StrictlyLowestCombinedPointsAboveFloor_OneIncrement()
    {
        var (worldState, modifiers, system) = SetUp();
        worldState.AddTribePoints(0, 10);
        worldState.AddTribePoints(1, 50);
        worldState.AddTribePoints(2, 50);
        worldState.AddTribePoints(3, 50);
        var zone = ZoneTestKit.CreateZone(MapId);

        system.Simulate(zone, 1);

        Assert.Equal(1, modifiers.GetDamageUpBonusIncrementCount(0));
        foreach (var other in (byte[])[1, 2, 3])
            Assert.Equal(0, modifiers.GetDamageUpBonusIncrementCount(other));
    }

    [Fact]
    public void SmallTribeFallback_BelowTheTenPointFloor_NeverEligible_EvenIfLowest()
    {
        var (worldState, modifiers, system) = SetUp();
        worldState.AddTribePoints(0, 9);
        worldState.AddTribePoints(1, 50);
        worldState.AddTribePoints(2, 50);
        worldState.AddTribePoints(3, 50);
        var zone = ZoneTestKit.CreateZone(MapId);

        system.Simulate(zone, 1);

        Assert.Equal(0, modifiers.GetDamageUpBonusIncrementCount(0));
    }

    [Fact]
    public void SmallTribeFallback_TiedLowestAmongEligibleTribes_NeitherGetsTheFallback()
    {
        var (worldState, modifiers, system) = SetUp();
        worldState.AddTribePoints(0, 10);
        worldState.AddTribePoints(1, 10);
        worldState.AddTribePoints(2, 50);
        worldState.AddTribePoints(3, 50);
        var zone = ZoneTestKit.CreateZone(MapId);

        system.Simulate(zone, 1);

        Assert.Equal(0, modifiers.GetDamageUpBonusIncrementCount(0));
        Assert.Equal(0, modifiers.GetDamageUpBonusIncrementCount(1));
    }

    [Fact]
    public void SmallTribeFallback_AllyPointsCombineForBothEligibilityAndComparison()
    {
        var (worldState, modifiers, system) = SetUp();
        worldState.SetAllianceOffer(0, 1, true);
        worldState.AddTribePoints(0, 10);
        worldState.AddTribePoints(2, 50);
        worldState.AddTribePoints(3, 50);
        var zone = ZoneTestKit.CreateZone(MapId);

        system.Simulate(zone, 1);

        Assert.Equal(0, modifiers.GetDamageUpBonusIncrementCount(0));
        Assert.Equal(0, modifiers.GetDamageUpBonusIncrementCount(1));
    }

    [Fact]
    public void OwningAnotherSlotAlready_SuppressesTheSmallTribeFallback_NeverCombinesWithIt()
    {
        var (worldState, modifiers, system) = SetUp();
        worldState.ResolveTribeSymbol(1, 0);
        worldState.AddTribePoints(0, 10);
        worldState.AddTribePoints(2, 50);
        worldState.AddTribePoints(3, 50);
        var zone = ZoneTestKit.CreateZone(MapId);

        system.Simulate(zone, 1);

        Assert.Equal(1, modifiers.GetDamageUpBonusIncrementCount(0));
    }
}
