using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.World.WorldState;

public class WorldStateServiceFormationAbilityTests
{
    private static WorldStateService CreateInitialized()
    {
        var service = new WorldStateService(new FakeWorldStateRepository(), NullLogger<WorldStateService>.Instance);
        service.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        return service;
    }

    [Fact]
    public void GetTribeFormationAbility_BeforeAnyDeclaration_IsInactiveForEveryTribe()
    {
        var service = CreateInitialized();

        for (byte tribeId = 0; tribeId < WorldStateService.TribeCount; tribeId++)
            Assert.Equal(0, service.GetTribeFormationAbility(tribeId));
    }

    [Fact]
    public void SetTribeFormationAbility_StoresTheCode_ReadableViaGet()
    {
        var service = CreateInitialized();

        service.SetTribeFormationAbility(2, 3);

        Assert.Equal(3, service.GetTribeFormationAbility(2));
    }

    [Fact]
    public void SetTribeFormationAbility_OnlyWritesTheTargetTribesOwnSlot()
    {
        var service = CreateInitialized();

        service.SetTribeFormationAbility(1, 1);

        Assert.Equal(0, service.GetTribeFormationAbility(0));
        Assert.Equal(1, service.GetTribeFormationAbility(1));
        Assert.Equal(0, service.GetTribeFormationAbility(2));
        Assert.Equal(0, service.GetTribeFormationAbility(3));
    }

    [Fact]
    public void SetTribeFormationAbility_ANewDeclaration_FullyReplacesThePreviousCode_NeverStacks()
    {
        var service = CreateInitialized();
        service.SetTribeFormationAbility(0, 1);

        service.SetTribeFormationAbility(0, 2);

        Assert.Equal(2, service.GetTribeFormationAbility(0));
    }

    [Fact]
    public void EndTribeSymbolBattle_ClearsEveryTribesFormationAbility_UnconditionallyIncludingAlreadyInactiveOnes()
    {
        var service = CreateInitialized();
        service.SetTribeFormationAbility(0, 1);
        service.SetTribeFormationAbility(3, 4);

        service.EndTribeSymbolBattle();

        for (byte tribeId = 0; tribeId < WorldStateService.TribeCount; tribeId++)
            Assert.Equal(0, service.GetTribeFormationAbility(tribeId));
    }

    [Fact]
    public void EndTribeSymbolBattle_DoesNotTouchTribeSymbolOwnership_OnlyFormationAbilityAndTheBattleFlag()
    {
        var service = CreateInitialized();
        service.StartTribeSymbolBattle();
        service.ResolveTribeSymbol(1, 2);
        service.SetTribeFormationAbility(1, 3);

        service.EndTribeSymbolBattle();

        Assert.False(service.World.TribeSymbolBattle);
        Assert.False(service.GetTribe(1).HasSymbol);
        Assert.Equal(0, service.GetTribeFormationAbility(1));
    }

    [Fact]
    public void GetTribeFormationAbility_InvalidTribeId_Throws()
    {
        var service = CreateInitialized();

        Assert.Throws<ArgumentOutOfRangeException>(() => service.GetTribeFormationAbility(4));
    }

    [Fact]
    public void SetTribeFormationAbility_InvalidTribeId_Throws()
    {
        var service = CreateInitialized();

        Assert.Throws<ArgumentOutOfRangeException>(() => service.SetTribeFormationAbility(4, 1));
    }

    [Fact]
    public void GetTribeFormationAbility_BeforeInitialize_Throws()
    {
        var service = new WorldStateService(new FakeWorldStateRepository(), NullLogger<WorldStateService>.Instance);

        Assert.Throws<InvalidOperationException>(() => service.GetTribeFormationAbility(0));
    }
}
