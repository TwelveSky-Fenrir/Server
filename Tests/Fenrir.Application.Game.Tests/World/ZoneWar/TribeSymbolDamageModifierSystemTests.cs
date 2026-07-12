using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class TribeSymbolDamageModifierSystemTests
{
    private const short MapId = 1;

    [Fact]
    public void FreshWorldState_EveryTribeHoldsItsOwnSymbol_NoPenaltyForAnyTribe()
    {
        var worldState = ZoneTestKit.CreateWorldState();
        var modifiers = new TribeSymbolCombatModifiers();
        var system = new TribeSymbolDamageModifierSystem(worldState, modifiers);
        var zone = ZoneTestKit.CreateZone(MapId);

        system.Simulate(zone, 1);

        for (byte tribeId = 0; tribeId < WorldStateService.TribeCount; tribeId++)
            Assert.Equal(0f, modifiers.GetDamageDownPenalty(tribeId));
    }

    [Fact]
    public void OneTribeLostItsSlot_OnlyThatTribeGetsThePenalty()
    {
        var worldState = ZoneTestKit.CreateWorldState();
        worldState.ResolveTribeSymbol(2, 1);
        var modifiers = new TribeSymbolCombatModifiers();
        var system = new TribeSymbolDamageModifierSystem(worldState, modifiers);
        var zone = ZoneTestKit.CreateZone(MapId);

        system.Simulate(zone, 1);

        Assert.Equal(TribeSymbolCombatModifiers.OwnSymbolLostDamageDownPenalty, modifiers.GetDamageDownPenalty(2));
        foreach (var other in (byte[])[0, 1, 3])
            Assert.Equal(0f, modifiers.GetDamageDownPenalty(other));
    }

    [Fact]
    public void EveryTribeLostItsOwnSlot_EveryTribePenalized()
    {
        var worldState = ZoneTestKit.CreateWorldState();
        for (byte tribeId = 0; tribeId < WorldStateService.TribeCount; tribeId++)
            worldState.ResolveTribeSymbol(tribeId, (byte)((tribeId + 1) % WorldStateService.TribeCount));
        var modifiers = new TribeSymbolCombatModifiers();
        var system = new TribeSymbolDamageModifierSystem(worldState, modifiers);
        var zone = ZoneTestKit.CreateZone(MapId);

        system.Simulate(zone, 1);

        for (byte tribeId = 0; tribeId < WorldStateService.TribeCount; tribeId++)
            Assert.Equal(TribeSymbolCombatModifiers.OwnSymbolLostDamageDownPenalty,
                modifiers.GetDamageDownPenalty(tribeId));
    }

    [Fact]
    public void RecomputesFromScratchEveryTick_ReflectsAMidTickChangeImmediately_NoSmoothing()
    {
        var worldState = ZoneTestKit.CreateWorldState();
        var modifiers = new TribeSymbolCombatModifiers();
        var system = new TribeSymbolDamageModifierSystem(worldState, modifiers);
        var zone = ZoneTestKit.CreateZone(MapId);

        system.Simulate(zone, 1);
        Assert.Equal(0f, modifiers.GetDamageDownPenalty(0));

        worldState.ResolveTribeSymbol(0, 3);
        system.Simulate(zone, 1);
        Assert.Equal(TribeSymbolCombatModifiers.OwnSymbolLostDamageDownPenalty, modifiers.GetDamageDownPenalty(0));

        worldState.ResolveTribeSymbol(0, 0);
        system.Simulate(zone, 1);
        Assert.Equal(0f, modifiers.GetDamageDownPenalty(0));
    }

    [Fact]
    public void UnconditionalOnEveryZone_ArbitraryZoneStillRecomputes()
    {
        var worldState = ZoneTestKit.CreateWorldState();
        worldState.ResolveTribeSymbol(1, 2);
        var modifiers = new TribeSymbolCombatModifiers();
        var system = new TribeSymbolDamageModifierSystem(worldState, modifiers);
        var arbitraryZone = ZoneTestKit.CreateZone(9999);

        system.Simulate(arbitraryZone, 1);

        Assert.Equal(TribeSymbolCombatModifiers.OwnSymbolLostDamageDownPenalty, modifiers.GetDamageDownPenalty(1));
    }

    [Fact]
    public void SlotLostToCurrentAlly_PenaltyIsWaived()
    {
        var worldState = ZoneTestKit.CreateWorldState();
        worldState.SetAllianceOffer(0, 1, true);
        worldState.ResolveTribeSymbol(0, 1);
        var modifiers = new TribeSymbolCombatModifiers();
        var system = new TribeSymbolDamageModifierSystem(worldState, modifiers);
        var zone = ZoneTestKit.CreateZone(MapId);

        system.Simulate(zone, 1);

        Assert.Equal(0f, modifiers.GetDamageDownPenalty(0));
    }

    [Fact]
    public void SlotLostToTribeThatIsNotTheCurrentAlly_FullPenaltyStillApplies()
    {
        var worldState = ZoneTestKit.CreateWorldState();
        worldState.SetAllianceOffer(0, 1, true);
        worldState.ResolveTribeSymbol(0, 2);
        var modifiers = new TribeSymbolCombatModifiers();
        var system = new TribeSymbolDamageModifierSystem(worldState, modifiers);
        var zone = ZoneTestKit.CreateZone(MapId);

        system.Simulate(zone, 1);

        Assert.Equal(TribeSymbolCombatModifiers.OwnSymbolLostDamageDownPenalty, modifiers.GetDamageDownPenalty(0));
    }

    [Fact]
    public void SlotLostWithNoCurrentAlly_FullPenaltyApplies()
    {
        var worldState = ZoneTestKit.CreateWorldState();
        worldState.ResolveTribeSymbol(0, 1);
        var modifiers = new TribeSymbolCombatModifiers();
        var system = new TribeSymbolDamageModifierSystem(worldState, modifiers);
        var zone = ZoneTestKit.CreateZone(MapId);

        system.Simulate(zone, 1);

        Assert.Equal(TribeSymbolCombatModifiers.OwnSymbolLostDamageDownPenalty, modifiers.GetDamageDownPenalty(0));
    }
}
