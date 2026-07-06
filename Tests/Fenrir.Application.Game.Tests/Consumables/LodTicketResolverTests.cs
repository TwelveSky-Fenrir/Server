using Fenrir.Application.Game.Domain.Consumables;
using Fenrir.Application.Game.Stats;

namespace Fenrir.Application.Game.Tests.Consumables;

public class LodTicketResolverTests
{
    [Fact]
    public void Resolve_LevelCapAndRebirth_IncrementsTheBankedRoundCounter()
    {
        var result = LodTicketResolver.Resolve(LevelProgressionCalculator.MaxLevel, rebirthCount: 1, slotQuantity: 1,
            currentLodRounds: 5);

        Assert.True(result.Succeeded);
        Assert.Equal(6, result.NewLodRounds);
    }

    [Fact]
    public void Resolve_BelowLevelCap_FailsCleanly()
    {
        var result = LodTicketResolver.Resolve((short)(LevelProgressionCalculator.MaxLevel - 1), rebirthCount: 1,
            slotQuantity: 1, currentLodRounds: 0);

        Assert.Equal(LodTicketResolver.Outcome.PreconditionFailed, result.Outcome);
        Assert.Equal(0, result.NewLodRounds);
    }

    [Fact]
    public void Resolve_NoRebirth_FailsCleanly()
    {
        var result = LodTicketResolver.Resolve(LevelProgressionCalculator.MaxLevel, rebirthCount: 0, slotQuantity: 1,
            currentLodRounds: 0);

        Assert.Equal(LodTicketResolver.Outcome.PreconditionFailed, result.Outcome);
    }

    [Fact]
    public void Resolve_EmptySlotQuantity_FailsCleanly()
    {
        var result = LodTicketResolver.Resolve(LevelProgressionCalculator.MaxLevel, rebirthCount: 1, slotQuantity: 0,
            currentLodRounds: 0);

        Assert.Equal(LodTicketResolver.Outcome.InsufficientQuantity, result.Outcome);
    }

    [Fact]
    public void Resolve_AtCeiling_Rejects_AndLeavesCounterUnchanged()
    {
        var result = LodTicketResolver.Resolve(LevelProgressionCalculator.MaxLevel, rebirthCount: 1, slotQuantity: 1,
            currentLodRounds: BankedCounterMath.GlobalCeiling);

        Assert.Equal(LodTicketResolver.Outcome.WouldExceedCeiling, result.Outcome);
        Assert.Equal(BankedCounterMath.GlobalCeiling, result.NewLodRounds);
    }
}
