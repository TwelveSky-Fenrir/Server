using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.World.WorldState;

public class WorldStateServiceTribeSymbolOwnerTests
{
    private static WorldStateService CreateInitialized()
    {
        var service = new WorldStateService(new FakeWorldStateRepository(), NullLogger<WorldStateService>.Instance);
        service.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        return service;
    }

    [Fact]
    public void AfterInitialize_EveryTribeOwnsItsOwnSlot()
    {
        var service = CreateInitialized();

        for (byte i = 0; i < WorldStateService.TribeCount; i++)
            Assert.Equal(i, service.GetTribeSymbolOwner(i));
    }

    [Fact]
    public void ResolveTribeSymbol_SlotOwnTribeLoses_OwnerBecomesTheWinningTribe()
    {
        var service = CreateInitialized();

        service.ResolveTribeSymbol(slotTribeId: 0, winnerTribeId: 2);

        Assert.Equal((byte)2, service.GetTribeSymbolOwner(0));
        Assert.False(service.GetTribe(0).HasSymbol);
    }

    [Fact]
    public void ResolveTribeSymbol_SlotOwnTribeKeepsIt_OwnerStaysTheSlotsOwnTribe()
    {
        var service = CreateInitialized();
        service.ResolveTribeSymbol(0, 2);

        service.ResolveTribeSymbol(0, 0);

        Assert.Equal((byte)0, service.GetTribeSymbolOwner(0));
        Assert.True(service.GetTribe(0).HasSymbol);
    }

    [Fact]
    public void ResolveTribeSymbol_CapturedByAThirdTribe_TracksTheCorrectChallengerIdentity()
    {
        var service = CreateInitialized();

        service.ResolveTribeSymbol(slotTribeId: 1, winnerTribeId: 3);

        Assert.Equal((byte)3, service.GetTribeSymbolOwner(1));
        Assert.Equal((byte)0, service.GetTribeSymbolOwner(0));
        Assert.Equal((byte)2, service.GetTribeSymbolOwner(2));
        Assert.Equal((byte)3, service.GetTribeSymbolOwner(3));
    }

    [Fact]
    public void StartTribeSymbolBattle_ResetsEverySlotBackToItsOwnTribe_EvenAfterCaptures()
    {
        var service = CreateInitialized();
        service.ResolveTribeSymbol(0, 2);
        service.ResolveTribeSymbol(1, 3);

        service.StartTribeSymbolBattle();

        for (byte i = 0; i < WorldStateService.TribeCount; i++)
            Assert.Equal(i, service.GetTribeSymbolOwner(i));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(255)]
    public void GetTribeSymbolOwner_OutOfRangeSlot_Throws(byte slotTribeId)
    {
        var service = CreateInitialized();

        Assert.Throws<ArgumentOutOfRangeException>(() => service.GetTribeSymbolOwner(slotTribeId));
    }

    [Fact]
    public void GetTribeSymbolOwner_BeforeInitialize_Throws()
    {
        var service = new WorldStateService(new FakeWorldStateRepository(), NullLogger<WorldStateService>.Instance);

        Assert.Throws<InvalidOperationException>(() => service.GetTribeSymbolOwner(0));
    }
}
