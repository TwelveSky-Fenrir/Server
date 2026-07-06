using Fenrir.Application.Login.Domain;

namespace Fenrir.Application.Login.Tests;

// op11 CL_LOGIN_SEND's two earliest gates (Server/ts25login/S04_MyWork02.cpp:149-160): maintenance lockdown
// (MaxPlayers == 0) evaluated before the server-full quota comparison, which itself rejects at "reached" not
// only "exceeded".
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
        // Guards against a naive ">=" reading that would call 0 >= 0 "full" instead of "maintenance" -- the
        // legacy source checks mMaxPlayerNum == 0 strictly first (S04_MyWork02.cpp:149-154) and never falls
        // through to the quota comparison at all in that case.
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
        // "Reached", not only "exceeded" (S04_MyWork02.cpp:155-160's ">=").
        var outcome = LoginCapacityGate.Evaluate(100, 100);

        Assert.Equal(LoginCapacityOutcome.ServerFull, outcome);
    }

    [Fact]
    public void Evaluate_CurrentPlayersAboveMax_ReturnsServerFull()
    {
        // An operator lowering the cap below the already-connected count: still full, no eviction happens here.
        var outcome = LoginCapacityGate.Evaluate(100, 150);

        Assert.Equal(LoginCapacityOutcome.ServerFull, outcome);
    }

    [Fact]
    public void Evaluate_NegativeMaxPlayers_FallsThroughToQuotaComparison_UnverifiedLegacyBranch()
    {
        // Not observed/exercised in the cited legacy range -- documented here only so this doesn't silently
        // regress to some other behavior later. A non-negative current count almost always meets/exceeds a
        // negative ceiling.
        var outcome = LoginCapacityGate.Evaluate(-1, 0);

        Assert.Equal(LoginCapacityOutcome.ServerFull, outcome);
    }
}
