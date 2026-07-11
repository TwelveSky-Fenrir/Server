using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class TribeQuotaGateTests
{
    [Theory]
    [InlineData(TribeQuotaGroup.None)]
    [InlineData(TribeQuotaGroup.RvrInstancedNoGate)]
    public void IsDeclaredTribeInRange_NoGateGroups_AcceptAnyDeclaredTribe(TribeQuotaGroup group)
    {
        Assert.True(TribeQuotaGate.IsDeclaredTribeInRange(group, -1));
        Assert.True(TribeQuotaGate.IsDeclaredTribeInRange(group, 0));
        Assert.True(TribeQuotaGate.IsDeclaredTribeInRange(group, 3));
        Assert.True(TribeQuotaGate.IsDeclaredTribeInRange(group, 99));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(-1, false)]
    [InlineData(3, false)]
    public void IsDeclaredTribeInRange_ThreeWay_ValidRangeIsZeroToTwo(int declaredTribe, bool expected)
    {
        Assert.Equal(expected, TribeQuotaGate.IsDeclaredTribeInRange(TribeQuotaGroup.ThreeWay, declaredTribe));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(3, true)]
    [InlineData(-1, false)]
    [InlineData(4, false)]
    public void IsDeclaredTribeInRange_FourWay_ValidRangeIsZeroToThree(int declaredTribe, bool expected)
    {
        Assert.Equal(expected, TribeQuotaGate.IsDeclaredTribeInRange(TribeQuotaGroup.FourWay, declaredTribe));
    }

    [Theory]
    [InlineData(TribeQuotaGroup.None)]
    [InlineData(TribeQuotaGroup.RvrInstancedNoGate)]
    public void Evaluate_NoGateGroups_AlwaysAccepted_RegardlessOfPopulation(TribeQuotaGroup group)
    {
        Assert.Equal(TribeQuotaOutcome.Accepted, TribeQuotaGate.Evaluate(group, 300,
            int.MaxValue));
    }

    [Theory]
    [InlineData(99, 99)]
    [InlineData(99, 33)]
    public void Evaluate_ThreeWay_AtOrAboveThreshold_IsQuotaFull(int maxConnections, int population)
    {
        Assert.Equal(TribeQuotaOutcome.QuotaFull,
            TribeQuotaGate.Evaluate(TribeQuotaGroup.ThreeWay, maxConnections, population));
    }

    [Fact]
    public void Evaluate_ThreeWay_OneUnderThreshold_IsAccepted()
    {
        Assert.Equal(TribeQuotaOutcome.Accepted, TribeQuotaGate.Evaluate(TribeQuotaGroup.ThreeWay, 99, 32));
    }

    [Fact]
    public void Evaluate_FourWay_UsesQuarterThreshold()
    {
        Assert.Equal(TribeQuotaOutcome.Accepted, TribeQuotaGate.Evaluate(TribeQuotaGroup.FourWay, 100, 24));
        Assert.Equal(TribeQuotaOutcome.QuotaFull, TribeQuotaGate.Evaluate(TribeQuotaGroup.FourWay, 100, 25));
    }

    [Fact]
    public void Evaluate_ThresholdComputation_UsesTruncatingIntegerDivision()
    {
        Assert.Equal(TribeQuotaOutcome.QuotaFull, TribeQuotaGate.Evaluate(TribeQuotaGroup.ThreeWay, 10, 3));
        Assert.Equal(TribeQuotaOutcome.Accepted, TribeQuotaGate.Evaluate(TribeQuotaGroup.ThreeWay, 10, 2));
    }
}
