using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Network.Serialization.Packets.Shared;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.World.WorldState;

/// <summary>
///     Covers <see cref="WorldStateProjection" />: WorldInfo's Zone038/tribe-symbol-battle/monster-symbol/
///     per-tribe symbol-ownership/points fields must reflect the live <see cref="WorldStateService" /> snapshot,
///     while every other WorldInfo field (no backing state modeled here) must pass through the template
///     unchanged.
/// </summary>
public class WorldStateProjectionTests
{
    private static WorldStateService CreateInitialized(FakeWorldStateRepository? repository = null)
    {
        var repo = repository ?? new FakeWorldStateRepository();
        var service = new WorldStateService(repo, NullLogger<WorldStateService>.Instance);
        service.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        return service;
    }

    [Fact]
    public void Apply_FreshWorldState_LeavesEveryProjectedFieldAtItsTemplateDefault()
    {
        var worldState = CreateInitialized();

        var result = WorldStateProjection.Apply(WorldStateTemplates.ZeroedWorldInfo, worldState);

        Assert.Equal(0, result.Zone038WinTribe);
        Assert.Equal(0, result.Zone038WinTribeTime);
        Assert.Equal(0, result.TribeSymbolBattle);
        Assert.Equal(0, result.MonsterSymbol);
        Assert.Equal(0, result.MonsterSymbolEndTime);
        Assert.Equal([1, 1, 1, 1], result.TribeSymbol); // fresh boot: every tribe starts owning its own symbol
        Assert.Equal([0, 0, 0, 0], result.TribePoint);

        // Untouched fields must still be the template's own zeroed defaults, not silently overwritten.
        Assert.Equal(WorldStateTemplates.ZeroedWorldInfo.TribeCloseInfo, result.TribeCloseInfo);
        Assert.Equal(WorldStateTemplates.ZeroedWorldInfo.PossibleAllianceInfo, result.PossibleAllianceInfo);
        Assert.Equal(WorldStateTemplates.ZeroedWorldInfo.GuildName1, result.GuildName1);
    }

    [Fact]
    public void Apply_Zone038WinnerRecorded_ReflectsWinnerAndTime()
    {
        var worldState = CreateInitialized();
        worldState.SetZone038Winner(2);

        var result = WorldStateProjection.Apply(WorldStateTemplates.ZeroedWorldInfo, worldState);

        Assert.Equal(2, result.Zone038WinTribe);
        Assert.NotEqual(0, result.Zone038WinTribeTime);
    }

    [Fact]
    public void Apply_TribeSymbolBattleOpened_SetsTheFlag()
    {
        var worldState = CreateInitialized();
        worldState.StartTribeSymbolBattle();

        var result = WorldStateProjection.Apply(WorldStateTemplates.ZeroedWorldInfo, worldState);

        Assert.Equal(1, result.TribeSymbolBattle);
    }

    [Fact]
    public void Apply_TribeSymbolResolvedAgainstTheSlotOwner_OnlyThatSlotLosesOwnership()
    {
        var worldState = CreateInitialized();

        // Slot 1 is contested and lost to tribe 3 -- only index 1 flips to 0, every other slot stays owned.
        worldState.ResolveTribeSymbol(slotTribeId: 1, winnerTribeId: 3);

        var result = WorldStateProjection.Apply(WorldStateTemplates.ZeroedWorldInfo, worldState);

        Assert.Equal([1, 0, 1, 1], result.TribeSymbol);
    }

    [Fact]
    public void Apply_MonsterSymbolResolved_ReflectsTheWinner()
    {
        var worldState = CreateInitialized();
        worldState.ResolveMonsterSymbol(3);

        var result = WorldStateProjection.Apply(WorldStateTemplates.ZeroedWorldInfo, worldState);

        Assert.Equal(3, result.MonsterSymbol);
        Assert.NotEqual(0, result.MonsterSymbolEndTime);
    }

    [Fact]
    public void Apply_TribePointsAdded_ReflectsEachTribesOwnTotal_Independently()
    {
        var worldState = CreateInitialized();
        worldState.AddTribePoints(0, 100);
        worldState.AddTribePoints(2, 250);

        var result = WorldStateProjection.Apply(WorldStateTemplates.ZeroedWorldInfo, worldState);

        Assert.Equal([100, 0, 250, 0], result.TribePoint);
    }

    [Fact]
    public void Apply_PassesGuildRankingProjectionFieldsThrough_WhenComposedTogether()
    {
        // WorldStateProjection.Apply and GuildRankingProjection.Apply overlay disjoint field sets onto the
        // same template -- composing them (as EnterWorldService does) must not clobber either's own overlay.
        var worldState = CreateInitialized();
        worldState.AddTribePoints(1, 42);

        var withGuildOverlay = WorldStateTemplates.ZeroedWorldInfo with { GuildName1 = "SomeGuild" };
        var result = WorldStateProjection.Apply(withGuildOverlay, worldState);

        Assert.Equal("SomeGuild", result.GuildName1);
        Assert.Equal([0, 42, 0, 0], result.TribePoint);
    }
}
