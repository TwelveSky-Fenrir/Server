using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.World.WorldState;

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
        Assert.Equal([0, 1, 2, 3], result.TribeSymbol);
        Assert.Equal([0, 0, 0, 0], result.TribePoint);

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

        worldState.ResolveTribeSymbol(1, 3);

        var result = WorldStateProjection.Apply(WorldStateTemplates.ZeroedWorldInfo, worldState);

        Assert.Equal([0, 0, 2, 3], result.TribeSymbol);
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
        var worldState = CreateInitialized();
        worldState.AddTribePoints(1, 42);

        var withGuildOverlay = WorldStateTemplates.ZeroedWorldInfo with { GuildName1 = "SomeGuild" };
        var result = WorldStateProjection.Apply(withGuildOverlay, worldState);

        Assert.Equal("SomeGuild", result.GuildName1);
        Assert.Equal([0, 42, 0, 0], result.TribePoint);
    }
}
