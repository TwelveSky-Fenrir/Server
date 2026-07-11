using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Tests.World.WorldState;

public class TribeBankTaxAccumulatorTests
{
    [Fact]
    public void CreditNpcServiceTax_Adds1Percent_TruncatedTowardZero()
    {
        var accumulator = new TribeBankTaxAccumulator();

        accumulator.CreditNpcServiceTax(0, 199);

        Assert.Equal(1, accumulator.GetTotal(0));
    }

    [Fact]
    public void CreditMonsterKillCurrencyTax_Adds9Percent_TruncatedTowardZero()
    {
        var accumulator = new TribeBankTaxAccumulator();

        accumulator.CreditMonsterKillCurrencyTax(2, 1111);

        Assert.Equal(99, accumulator.GetTotal(2));
    }

    [Fact]
    public void Credits_ToDifferentTribes_AccumulateIndependently()
    {
        var accumulator = new TribeBankTaxAccumulator();

        accumulator.CreditNpcServiceTax(0, 1000);
        accumulator.CreditNpcServiceTax(1, 2000);
        accumulator.CreditMonsterKillCurrencyTax(0, 1000);

        Assert.Equal(10 + 90, accumulator.GetTotal(0));
        Assert.Equal(20, accumulator.GetTotal(1));
        Assert.Equal(0, accumulator.GetTotal(2));
        Assert.Equal(0, accumulator.GetTotal(3));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(255)]
    public void Credit_WithOutOfRangeActingTribe_IsSilentlySkipped_NoThrow(byte outOfRangeTribe)
    {
        var accumulator = new TribeBankTaxAccumulator();

        accumulator.CreditNpcServiceTax(outOfRangeTribe, 1_000_000);
        accumulator.CreditMonsterKillCurrencyTax(outOfRangeTribe, 1_000_000);

        for (byte tribe = 0; tribe < TribeBankTaxAccumulator.TribeCount; tribe++)
            Assert.Equal(0, accumulator.GetTotal(tribe));
    }

    [Fact]
    public void GetTotal_WithOutOfRangeTribe_Throws()
    {
        var accumulator = new TribeBankTaxAccumulator();

        Assert.Throws<ArgumentOutOfRangeException>(() => accumulator.GetTotal(4));
    }

    [Fact]
    public void Credit_ZeroBaseAmount_AddsNothing()
    {
        var accumulator = new TribeBankTaxAccumulator();

        accumulator.CreditNpcServiceTax(0, 0);

        Assert.Equal(0, accumulator.GetTotal(0));
    }

    [Fact]
    public void Credit_WouldExceedZoneLocalCeiling_WholeAdditionIsDroppedSilently()
    {
        var accumulator = new TribeBankTaxAccumulator();

        accumulator.CreditMonsterKillCurrencyTax(0,
            (long)((TribeBankTaxAccumulator.ZoneLocalCeiling - 50) /
                   TribeBankTaxAccumulator.MonsterKillCurrencyTaxRate));
        var before = accumulator.GetTotal(0);
        Assert.True(before <= TribeBankTaxAccumulator.ZoneLocalCeiling - 50);

        accumulator.CreditMonsterKillCurrencyTax(0, 10_000_000);

        Assert.Equal(before, accumulator.GetTotal(0));
    }

    [Fact]
    public void Credit_NearCeiling_OneMoreNonzeroCreditOverflowsAndIsDropped()
    {
        var accumulator = new TribeBankTaxAccumulator();

        const long halfCeilingTax = TribeBankTaxAccumulator.ZoneLocalCeiling / 2;
        var baseAmountForHalfCeiling = (long)(halfCeilingTax / TribeBankTaxAccumulator.MonsterKillCurrencyTaxRate);

        accumulator.CreditMonsterKillCurrencyTax(0, baseAmountForHalfCeiling);
        accumulator.CreditMonsterKillCurrencyTax(0, baseAmountForHalfCeiling);

        var afterTwoHalves = accumulator.GetTotal(0);
        Assert.InRange(afterTwoHalves, TribeBankTaxAccumulator.ZoneLocalCeiling - 10,
            TribeBankTaxAccumulator.ZoneLocalCeiling);

        accumulator.CreditMonsterKillCurrencyTax(0, 1000);
        Assert.Equal(afterTwoHalves, accumulator.GetTotal(0));
    }

    [Fact]
    public void ResolveBeneficiaryTribe_Indirection_RedirectsTheCreditToTheResolvedSlot()
    {
        var accumulator = new TribeBankTaxAccumulator(tribe => tribe == 0 ? (byte)2 : tribe);

        accumulator.CreditNpcServiceTax(0, 1000);

        Assert.Equal(0, accumulator.GetTotal(0));
        Assert.Equal(10, accumulator.GetTotal(2));
    }

    [Fact]
    public void DefaultConstructor_HasIdentityIndirection_ByDefault()
    {
        var accumulator = new TribeBankTaxAccumulator();

        accumulator.CreditNpcServiceTax(3, 1000);

        Assert.Equal(10, accumulator.GetTotal(3));
    }

    [Fact]
    public void TrySweep_BeforeTenMinutesOfZoneUptime_IsNotDue()
    {
        var accumulator = new TribeBankTaxAccumulator();
        accumulator.CreditNpcServiceTax(0, 1000);

        var due = accumulator.TrySweep(TimeSpan.FromMinutes(9) + TimeSpan.FromSeconds(59), out var payload);

        Assert.False(due);
        Assert.True(payload.IsEmpty);
        Assert.Equal(10, accumulator.GetTotal(0));
    }

    [Fact]
    public void TrySweep_AtTenMinutesOfZoneUptime_IsDue_SnapshotsAndResetsUnconditionally()
    {
        var accumulator = new TribeBankTaxAccumulator();
        accumulator.CreditNpcServiceTax(0, 1000);
        accumulator.CreditNpcServiceTax(1, 2000);
        accumulator.CreditMonsterKillCurrencyTax(2, 1000);
        accumulator.CreditNpcServiceTax(3, 500);

        var due = accumulator.TrySweep(TimeSpan.FromMinutes(10), out var payload);

        Assert.True(due);
        Assert.Equal(10, payload.Tribe0);
        Assert.Equal(20, payload.Tribe1);
        Assert.Equal(90, payload.Tribe2);
        Assert.Equal(5, payload.Tribe3);

        for (byte tribe = 0; tribe < TribeBankTaxAccumulator.TribeCount; tribe++)
            Assert.Equal(0, accumulator.GetTotal(tribe));
    }

    [Fact]
    public void TrySweep_FirstSweep_IsRelativeToZeroUptime_NotWallClock()
    {
        var accumulator = new TribeBankTaxAccumulator();

        Assert.False(accumulator.TrySweep(TimeSpan.Zero, out _));

        Assert.True(accumulator.TrySweep(TimeSpan.FromMinutes(10), out _));
    }

    [Fact]
    public void TrySweep_SecondSweep_IsRelativeToThePreviousSweep_NotToUptimeZero()
    {
        var accumulator = new TribeBankTaxAccumulator();
        Assert.True(accumulator.TrySweep(TimeSpan.FromMinutes(10), out _));

        Assert.False(accumulator.TrySweep(TimeSpan.FromMinutes(15), out _));

        Assert.True(accumulator.TrySweep(TimeSpan.FromMinutes(20), out _));
    }

    [Fact]
    public void TrySweep_WithNothingAccumulated_StillFiresAndReturnsAnEmptyPayload()
    {
        var accumulator = new TribeBankTaxAccumulator();

        var due = accumulator.TrySweep(TimeSpan.FromMinutes(10), out var payload);

        Assert.True(due);
        Assert.True(payload.IsEmpty);
    }
}
