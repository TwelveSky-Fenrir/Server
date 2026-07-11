using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.WorldState;

public class ZoneTribeBankTaxTests
{
    [Fact]
    public void CreditMonsterKillTribeTax_UpdatesTheZoneLocalRunningTotal()
    {
        var zone = ZoneTestKit.CreateZone(1);

        zone.CreditMonsterKillTribeTax(0, 1000);

        Assert.Equal(90, zone.GetTribeBankTaxTotal(0));
        Assert.Equal(0, zone.GetTribeBankTaxTotal(1));
    }

    [Fact]
    public void CreditNpcServiceTribeTax_UpdatesTheZoneLocalRunningTotal()
    {
        var zone = ZoneTestKit.CreateZone(1);

        zone.CreditNpcServiceTribeTax(2, 1000);

        Assert.Equal(10, zone.GetTribeBankTaxTotal(2));
    }

    [Fact]
    public void CreditWithOutOfRangeTribe_IsSilentlySkipped()
    {
        var zone = ZoneTestKit.CreateZone(1);

        zone.CreditMonsterKillTribeTax(200, 1_000_000);

        for (byte tribe = 0; tribe < 4; tribe++)
            Assert.Equal(0, zone.GetTribeBankTaxTotal(tribe));
    }

    [Fact]
    public void Tick_BeforeTenMinutesOfZoneUptime_NeverQueuesASweep()
    {
        var zone = ZoneTestKit.CreateZone(1);
        zone.CreditNpcServiceTribeTax(0, 1000);

        zone.Tick(TimeSpan.FromMinutes(9));

        Assert.Empty(zone.DrainPendingTribeBankTaxSweeps());
        Assert.Equal(10, zone.GetTribeBankTaxTotal(0));
    }

    [Fact]
    public void Tick_AtTenMinutesOfZoneUptime_QueuesASweep_AndResetsTheZoneLocalTotalsImmediately()
    {
        var zone = ZoneTestKit.CreateZone(1);
        zone.CreditNpcServiceTribeTax(0, 1000);
        zone.CreditMonsterKillTribeTax(1, 1000);

        zone.Tick(TimeSpan.FromMinutes(10));

        var sweeps = zone.DrainPendingTribeBankTaxSweeps();
        var sweep = Assert.Single(sweeps);
        Assert.Equal(10, sweep.Tribe0);
        Assert.Equal(90, sweep.Tribe1);
        Assert.Equal(0, sweep.Tribe2);
        Assert.Equal(0, sweep.Tribe3);

        for (byte tribe = 0; tribe < 4; tribe++)
            Assert.Equal(0, zone.GetTribeBankTaxTotal(tribe));
    }

    [Fact]
    public void Tick_WithNothingAccumulated_StillQueuesAnEmptySweepAtTheTenMinuteMark()
    {
        var zone = ZoneTestKit.CreateZone(1);

        zone.Tick(TimeSpan.FromMinutes(10));

        var sweep = Assert.Single(zone.DrainPendingTribeBankTaxSweeps());
        Assert.True(sweep.IsEmpty);
    }

    [Fact]
    public void DrainPendingTribeBankTaxSweeps_IsEmptyUntilASweepBecomesDue()
    {
        var zone = ZoneTestKit.CreateZone(1);
        zone.CreditNpcServiceTribeTax(0, 1000);

        zone.Tick(SimulationClock.LegacyTick);

        Assert.Empty(zone.DrainPendingTribeBankTaxSweeps());
    }

    [Fact]
    public void CustomAccumulator_WithTribeSymbolIndirection_IsHonoredThroughZone()
    {
        var accumulator = new TribeBankTaxAccumulator(tribe => tribe == 1 ? (byte)3 : tribe);
        var zone = ZoneTestKit.CreateZone(1, tribeBankTax: accumulator);

        zone.CreditNpcServiceTribeTax(1, 1000);

        Assert.Equal(0, zone.GetTribeBankTaxTotal(1));
        Assert.Equal(10, zone.GetTribeBankTaxTotal(3));
    }
}
