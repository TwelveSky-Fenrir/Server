using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Tests.World.WorldState;

public class TribeBankTaxAccumulatorTests
{
    [Fact]
    public void CreditNpcServiceTax_Adds1Percent_TruncatedTowardZero()
    {
        var accumulator = new TribeBankTaxAccumulator();

        // 1% of 199 == 1.99 -> truncates to 1, not 2.
        accumulator.CreditNpcServiceTax(0, 199);

        Assert.Equal(1, accumulator.GetTotal(0));
    }

    [Fact]
    public void CreditMonsterKillCurrencyTax_Adds9Percent_TruncatedTowardZero()
    {
        var accumulator = new TribeBankTaxAccumulator();

        // 9% of 1111 == 99.99 -> truncates to 99.
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

        // Nothing anywhere was credited -- every in-range slot is still zero.
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

        // Push tribe 0 to just 50 below the ceiling, then credit an event whose tax (100) would push it over.
        accumulator.CreditMonsterKillCurrencyTax(0,
            (long)((TribeBankTaxAccumulator.ZoneLocalCeiling - 50) /
                   TribeBankTaxAccumulator.MonsterKillCurrencyTaxRate));
        var before = accumulator.GetTotal(0);
        Assert.True(before <= TribeBankTaxAccumulator.ZoneLocalCeiling - 50);

        // 9% of 10_000_000 == 900_000, comfortably enough to push any near-ceiling total over.
        accumulator.CreditMonsterKillCurrencyTax(0, 10_000_000);

        Assert.Equal(before, accumulator.GetTotal(0)); // unchanged -- the whole addition was dropped.
    }

    [Fact]
    public void Credit_NearCeiling_OneMoreNonzeroCreditOverflowsAndIsDropped()
    {
        var accumulator = new TribeBankTaxAccumulator();

        // Two credits whose 9% tax is exactly half the ceiling each -- avoids relying on floating-point
        // precision landing exactly on the full ceiling from a single computed base amount.
        const long halfCeilingTax = TribeBankTaxAccumulator.ZoneLocalCeiling / 2;
        var baseAmountForHalfCeiling = (long)(halfCeilingTax / TribeBankTaxAccumulator.MonsterKillCurrencyTaxRate);

        accumulator.CreditMonsterKillCurrencyTax(0, baseAmountForHalfCeiling);
        accumulator.CreditMonsterKillCurrencyTax(0, baseAmountForHalfCeiling);

        var afterTwoHalves = accumulator.GetTotal(0);
        Assert.InRange(afterTwoHalves, TribeBankTaxAccumulator.ZoneLocalCeiling - 10,
            TribeBankTaxAccumulator.ZoneLocalCeiling);

        // Any further nonzero tax now overflows the ceiling -- dropped, total unchanged.
        accumulator.CreditMonsterKillCurrencyTax(0, 1000);
        Assert.Equal(afterTwoHalves, accumulator.GetTotal(0));
    }

    [Fact]
    public void ResolveBeneficiaryTribe_Indirection_RedirectsTheCreditToTheResolvedSlot()
    {
        // Tribe 0's own slot has been won by tribe 2 (Zone038 tribe-symbol-battle reassignment stand-in).
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
        Assert.Equal(10, accumulator.GetTotal(0)); // untouched
    }

    [Fact]
    public void TrySweep_AtTenMinutesOfZoneUptime_IsDue_SnapshotsAndResetsUnconditionally()
    {
        var accumulator = new TribeBankTaxAccumulator();
        accumulator.CreditNpcServiceTax(0, 1000); // 10
        accumulator.CreditNpcServiceTax(1, 2000); // 20
        accumulator.CreditMonsterKillCurrencyTax(2, 1000); // 90
        accumulator.CreditNpcServiceTax(3, 500); // 5

        var due = accumulator.TrySweep(TimeSpan.FromMinutes(10), out var payload);

        Assert.True(due);
        Assert.Equal(10, payload.Tribe0);
        Assert.Equal(20, payload.Tribe1);
        Assert.Equal(90, payload.Tribe2);
        Assert.Equal(5, payload.Tribe3);

        // Reset happened unconditionally, in the same step -- no residual state left over.
        for (byte tribe = 0; tribe < TribeBankTaxAccumulator.TribeCount; tribe++)
            Assert.Equal(0, accumulator.GetTotal(tribe));
    }

    [Fact]
    public void TrySweep_FirstSweep_IsRelativeToZeroUptime_NotWallClock()
    {
        var accumulator = new TribeBankTaxAccumulator();

        // Called with zero elapsed uptime: not due yet, even though this is the very first call.
        Assert.False(accumulator.TrySweep(TimeSpan.Zero, out _));

        // Exactly 10 minutes after process start (uptime clock, not wall clock): due.
        Assert.True(accumulator.TrySweep(TimeSpan.FromMinutes(10), out _));
    }

    [Fact]
    public void TrySweep_SecondSweep_IsRelativeToThePreviousSweep_NotToUptimeZero()
    {
        var accumulator = new TribeBankTaxAccumulator();
        Assert.True(accumulator.TrySweep(TimeSpan.FromMinutes(10), out _));

        // Only 5 more minutes since the last sweep -- not due yet, even though total uptime (15 min) is high.
        Assert.False(accumulator.TrySweep(TimeSpan.FromMinutes(15), out _));

        // A further 5 minutes (20 min total, 10 since the last sweep) -- due again.
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
