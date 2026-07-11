using Fenrir.Application.Login.Domain;

namespace Fenrir.Application.Login.Tests;

public class LoginCapacityGateTests
{
    [Fact]
    public void Evaluate_MaxPlayersIsZero_ReturnsMaintenance()
    {
        var outcome = LoginCapacityGate.Evaluate(0, 0);

        Assert.Equal(LoginCapacityOutcome.Maintenance, outcome);
    }

    [Fact]
    public void Evaluate_MaxPlayersIsZero_ReturnsMaintenance_EvenWhenCurrentPlayersIsAlsoZero_NotServerFull()
    {
        var outcome = LoginCapacityGate.Evaluate(0, 5);

        Assert.Equal(LoginCapacityOutcome.Maintenance, outcome);
    }

    [Fact]
    public void Evaluate_CurrentPlayersBelowMax_ReturnsAllowed()
    {
        var outcome = LoginCapacityGate.Evaluate(100, 99);

        Assert.Equal(LoginCapacityOutcome.Allowed, outcome);
    }

    [Fact]
    public void Evaluate_CurrentPlayersEqualsMax_ReturnsServerFull()
    {
        var outcome = LoginCapacityGate.Evaluate(100, 100);

        Assert.Equal(LoginCapacityOutcome.ServerFull, outcome);
    }

    [Fact]
    public void Evaluate_CurrentPlayersAboveMax_ReturnsServerFull()
    {
        var outcome = LoginCapacityGate.Evaluate(100, 150);

        Assert.Equal(LoginCapacityOutcome.ServerFull, outcome);
    }

    [Fact]
    public void Evaluate_NegativeMaxPlayers_FallsThroughToQuotaComparison_UnverifiedLegacyBranch()
    {
        var outcome = LoginCapacityGate.Evaluate(-1, 0);

        Assert.Equal(LoginCapacityOutcome.ServerFull, outcome);
    }
}
