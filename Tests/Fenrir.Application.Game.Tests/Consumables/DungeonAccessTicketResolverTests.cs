using Fenrir.Application.Game.Domain.Consumables;

namespace Fenrir.Application.Game.Tests.Consumables;

public class DungeonAccessTicketResolverTests
{
    [Fact]
    public void Resolve_EliteDungeonTicketLarge_AddsOneHundredEighty()
    {
        var result = DungeonAccessTicketResolver.Resolve(0,
            DungeonAccessTicketResolver.EliteDungeonTicketLargeAmount, slotQuantity: 1);

        Assert.True(result.Succeeded);
        Assert.Equal(180, result.NewCounterValue);
    }

    [Fact]
    public void Resolve_EliteDungeonTicketMedium_AddsOneHundredTwenty()
    {
        var result = DungeonAccessTicketResolver.Resolve(10,
            DungeonAccessTicketResolver.EliteDungeonTicketMediumAmount, slotQuantity: 1);

        Assert.True(result.Succeeded);
        Assert.Equal(130, result.NewCounterValue);
    }

    [Fact]
    public void Resolve_EliteDungeonTicketSmall_AddsSixty()
    {
        var result = DungeonAccessTicketResolver.Resolve(0,
            DungeonAccessTicketResolver.EliteDungeonTicketSmallAmount, slotQuantity: 1);

        Assert.True(result.Succeeded);
        Assert.Equal(60, result.NewCounterValue);
    }

    [Fact]
    public void Resolve_DungeonKey_AddsOne()
    {
        var result = DungeonAccessTicketResolver.Resolve(5, DungeonAccessTicketResolver.DungeonKeyAmount,
            slotQuantity: 1);

        Assert.True(result.Succeeded);
        Assert.Equal(6, result.NewCounterValue);
    }

    [Fact]
    public void Resolve_IvyHallTicketSmall_AddsOneHundredEighty()
    {
        var result = DungeonAccessTicketResolver.Resolve(0,
            DungeonAccessTicketResolver.IvyHallTicketSmallAmount, slotQuantity: 1);

        Assert.True(result.Succeeded);
        Assert.Equal(180, result.NewCounterValue);
    }

    [Fact]
    public void Resolve_IvyHallTicketLarge_AddsThreeHundredSixty()
    {
        var result = DungeonAccessTicketResolver.Resolve(0,
            DungeonAccessTicketResolver.IvyHallTicketLargeAmount, slotQuantity: 1);

        Assert.True(result.Succeeded);
        Assert.Equal(360, result.NewCounterValue);
    }

    [Fact]
    public void Resolve_ZeroSlotQuantity_ReportsInsufficientQuantity_CounterUntouched()
    {
        var result = DungeonAccessTicketResolver.Resolve(50, 180, slotQuantity: 0);

        Assert.Equal(DungeonAccessTicketResolver.Outcome.InsufficientQuantity, result.Outcome);
        Assert.Equal(50, result.NewCounterValue);
    }

    [Fact]
    public void Resolve_WouldExceedCeiling_Rejects_CounterUntouched()
    {
        var result = DungeonAccessTicketResolver.Resolve(BankedCounterMath.GlobalCeiling, 180, slotQuantity: 1);

        Assert.Equal(DungeonAccessTicketResolver.Outcome.WouldExceedCeiling, result.Outcome);
        Assert.Equal(BankedCounterMath.GlobalCeiling, result.NewCounterValue);
    }

    [Fact]
    public void Resolve_CustomCeiling_UsedInsteadOfGlobalDefault_WithinBounds_Succeeds()
    {
        var result = DungeonAccessTicketResolver.Resolve(70, 20, slotQuantity: 1, ceiling: 100);

        Assert.True(result.Succeeded);
        Assert.Equal(90, result.NewCounterValue);
    }

    [Fact]
    public void Resolve_CustomCeiling_ExceededByProjectedTotal_Rejects()
    {
        var result = DungeonAccessTicketResolver.Resolve(90, 20, slotQuantity: 1, ceiling: 100);

        Assert.Equal(DungeonAccessTicketResolver.Outcome.WouldExceedCeiling, result.Outcome);
        Assert.Equal(90, result.NewCounterValue);
    }
}
