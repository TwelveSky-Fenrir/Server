using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

/// <summary>
///     Covers <see cref="TribeSymbolDamageModifierSystem" /> -- <c>AdjustSymbolDamageInfo</c>'s damage-down
///     half, recomputed from scratch every tick for all four tribes.
/// </summary>
public class TribeSymbolDamageModifierSystemTests
{
    private const short MapId = 1;

    [Fact]
    public void FreshWorldState_EveryTribeHoldsItsOwnSymbol_NoPenaltyForAnyTribe()
    {
        // FakeWorldStateRepository seeds every tribe with HasSymbol=true, matching a freshly-initialized
        // usp_WorldState_EnsureInitialized boot.
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
        worldState.ResolveTribeSymbol(2, 1); // tribe 2's own slot lost to tribe 1
        var modifiers = new TribeSymbolCombatModifiers();
        var system = new TribeSymbolDamageModifierSystem(worldState, modifiers);
        var zone = ZoneTestKit.CreateZone(MapId);

        system.Simulate(zone, 1);

        Assert.Equal(TribeSymbolCombatModifiers.OwnSymbolLostDamageDownPenalty, modifiers.GetDamageDownPenalty(2));
        foreach (byte other in (byte[]) [0, 1, 3])
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

        worldState.ResolveTribeSymbol(0, 3); // tribe 0 loses its own slot to tribe 3
        system.Simulate(zone, 1);
        Assert.Equal(TribeSymbolCombatModifiers.OwnSymbolLostDamageDownPenalty, modifiers.GetDamageDownPenalty(0));

        worldState.ResolveTribeSymbol(0, 0); // tribe 0 recaptures its own slot on the very next tick
        system.Simulate(zone, 1);
        Assert.Equal(0f, modifiers.GetDamageDownPenalty(0));
    }

    [Fact]
    public void UnconditionalOnEveryZone_ArbitraryZoneStillRecomputes()
    {
        // No map-id gate exists on this system at all -- unlike the multi-instance RvR schedulers, every zone
        // this shard hosts recomputes the same shared TribeSymbolCombatModifiers state, regardless of MapId.
        var worldState = ZoneTestKit.CreateWorldState();
        worldState.ResolveTribeSymbol(1, 2);
        var modifiers = new TribeSymbolCombatModifiers();
        var system = new TribeSymbolDamageModifierSystem(worldState, modifiers);
        var arbitraryZone = ZoneTestKit.CreateZone(9999);

        system.Simulate(arbitraryZone, 1);

        Assert.Equal(TribeSymbolCombatModifiers.OwnSymbolLostDamageDownPenalty, modifiers.GetDamageDownPenalty(1));
    }
}
