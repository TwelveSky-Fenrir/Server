using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

/// <summary>
///     Covers <see cref="TribeSymbolDamageModifierSystem" />'s new (B15, wave15 contract) damage-up bonus
///     INCREMENT COUNT half -- <see cref="TribeSymbolCombatModifiers.GetDamageUpBonusIncrementCount" />. Every
///     gate/threshold exercised here is a literal number the wave15 contract's own prose states (the four-
///     increment ceiling, the ten-point small-tribe floor); the per-increment FLAT DAMAGE magnitude itself is
///     deliberately not modeled anywhere in this codebase yet -- see <see cref="TribeSymbolCombatModifiers" />'s
///     own remarks for why -- so this class only asserts on the COUNT, never on damage.
/// </summary>
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
        // Every tribe owns its own slot (gate passes), but none of the other three slots or the monster
        // symbol, and every tribe starts at 0 points -- below the ten-point small-tribe floor, so the
        // fallback in (e) is unreachable for anyone either.
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
        worldState.ResolveTribeSymbol(0, 1); // tribe 0 loses its own slot to tribe 1
        worldState.AddTribePoints(0, 100); // even with plenty of points...
        var zone = ZoneTestKit.CreateZone(MapId);

        system.Simulate(zone, 1);

        // ...tribe 0 still gets zero: the own-slot gate (b) is not met, so neither (c)/(d) nor the (e)
        // fallback can ever apply.
        Assert.Equal(0, modifiers.GetDamageUpBonusIncrementCount(0));
    }

    [Fact]
    public void OwnsOneOtherSlot_OneIncrement()
    {
        var (worldState, modifiers, system) = SetUp();
        worldState.ResolveTribeSymbol(1, 0); // tribe 0 captures tribe 1's own slot
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
        worldState.ResolveMonsterSymbol(1); // tribe 1 owns the monster symbol, not tribe 0
        var zone = ZoneTestKit.CreateZone(MapId);

        system.Simulate(zone, 1);

        Assert.Equal(0, modifiers.GetDamageUpBonusIncrementCount(0));
    }

    [Fact]
    public void AllySlotControlCountsTowardTheOwnersBonus_ButNotTheAllysOwnUnrelatedGate()
    {
        var (worldState, modifiers, system) = SetUp();
        worldState.SetAllianceOffer(0, 1, isAccepted: true); // tribes 0 and 1 are allied
        worldState.ResolveTribeSymbol(2, 1); // ally (tribe 1) captures tribe 2's own slot
        var zone = ZoneTestKit.CreateZone(MapId);

        system.Simulate(zone, 1);

        // Tribe 0 still owns its own slot AND its ally (1) controls slot 2 -- one increment via the ally.
        Assert.Equal(1, modifiers.GetDamageUpBonusIncrementCount(0));
        // Tribe 1 (the ally) also benefits symmetrically: it owns its own slot directly and controls slot 2
        // directly (not via ally) -- also one increment, for the same underlying fact.
        Assert.Equal(1, modifiers.GetDamageUpBonusIncrementCount(1));
    }

    [Fact]
    public void SmallTribeFallback_StrictlyLowestCombinedPointsAboveFloor_OneIncrement()
    {
        var (worldState, modifiers, system) = SetUp();
        worldState.AddTribePoints(0, 10); // exactly at the floor -- still eligible
        worldState.AddTribePoints(1, 50);
        worldState.AddTribePoints(2, 50);
        worldState.AddTribePoints(3, 50);
        var zone = ZoneTestKit.CreateZone(MapId);

        system.Simulate(zone, 1);

        // Tribe 0 owns only its own slot (gate passes, (c)/(d) produce nothing) and is the strict lowest
        // among the four, all of which clear the ten-point floor -- fallback grants exactly one increment.
        Assert.Equal(1, modifiers.GetDamageUpBonusIncrementCount(0));
        foreach (var other in (byte[])[1, 2, 3])
            Assert.Equal(0, modifiers.GetDamageUpBonusIncrementCount(other));
    }

    [Fact]
    public void SmallTribeFallback_BelowTheTenPointFloor_NeverEligible_EvenIfLowest()
    {
        var (worldState, modifiers, system) = SetUp();
        worldState.AddTribePoints(0, 9); // one short of the floor
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
        worldState.AddTribePoints(1, 10); // tied with tribe 0 for lowest
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
        worldState.SetAllianceOffer(0, 1, isAccepted: true); // tribes 0+1 combine to 10+0=10 (eligible, exactly floor)
        worldState.AddTribePoints(0, 10);
        worldState.AddTribePoints(2, 50);
        worldState.AddTribePoints(3, 50);
        var zone = ZoneTestKit.CreateZone(MapId);

        system.Simulate(zone, 1);

        // Tribe 0's own combined total (10 + ally's 0 = 10) is the strict lowest among all four combined
        // totals (tribe 1's own combined total is identical, 0 + 10 = 10, but tribe 1 doesn't independently
        // own its own slot's gate any differently -- both members of the alliance are simultaneously eligible
        // and tied with each other, so this asserts the SAME outcome as an ordinary tie: neither gets it).
        Assert.Equal(0, modifiers.GetDamageUpBonusIncrementCount(0));
        Assert.Equal(0, modifiers.GetDamageUpBonusIncrementCount(1));
    }

    [Fact]
    public void OwningAnotherSlotAlready_SuppressesTheSmallTribeFallback_NeverCombinesWithIt()
    {
        var (worldState, modifiers, system) = SetUp();
        worldState.ResolveTribeSymbol(1, 0); // tribe 0 already has one increment via (c)
        worldState.AddTribePoints(0, 10);
        worldState.AddTribePoints(2, 50);
        worldState.AddTribePoints(3, 50);
        // Tribe 1 lost its own slot, so it can never itself be a fallback candidate (gate (b) fails for it).
        var zone = ZoneTestKit.CreateZone(MapId);

        system.Simulate(zone, 1);

        // Exactly 1 (from (c) alone) -- the fallback in (e) never fires on top of an already-nonzero (c)/(d).
        Assert.Equal(1, modifiers.GetDamageUpBonusIncrementCount(0));
    }
}
