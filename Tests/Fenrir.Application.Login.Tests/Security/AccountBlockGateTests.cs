using Fenrir.Application.Login.Domain.Security;

namespace Fenrir.Application.Login.Tests.Security;

public class AccountBlockGateTests
{
    private static readonly DateTime NowUtc = new(2026, 7, 12, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NoBanNoLockout_IsAllowed()
    {
        Assert.Equal(AccountBlockOutcome.Allowed,
            AccountBlockGate.Evaluate(false, false, null, NowUtc));
    }

    [Fact]
    public void IsBannedFlagSet_IsAdminBanned()
    {
        Assert.Equal(AccountBlockOutcome.AdminBanned,
            AccountBlockGate.Evaluate(true, false, null, NowUtc));
    }

    [Fact]
    public void ActiveBanLogEntry_IsAdminBanned_EvenWhenIsBannedFlagIsFalse()
    {
        Assert.Equal(AccountBlockOutcome.AdminBanned,
            AccountBlockGate.Evaluate(false, true, null, NowUtc));
    }

    [Fact]
    public void LockoutUntilUtcInTheFuture_IsAutoLockedOut()
    {
        Assert.Equal(AccountBlockOutcome.AutoLockedOut,
            AccountBlockGate.Evaluate(false, false, NowUtc.AddMinutes(1), NowUtc));
    }

    [Fact]
    public void LockoutUntilUtcInThePast_IsAllowed()
    {
        Assert.Equal(AccountBlockOutcome.Allowed,
            AccountBlockGate.Evaluate(false, false, NowUtc.AddMinutes(-1), NowUtc));
    }

    [Fact]
    public void LockoutUntilUtcNull_IsAllowed()
    {
        Assert.Equal(AccountBlockOutcome.Allowed,
            AccountBlockGate.Evaluate(false, false, null, NowUtc));
    }

    [Fact]
    public void AdminBanAndAutoLockoutBothActive_AdminBanWins_AndIsReportedDistinctlyFromAutoLockout()
    {
        Assert.Equal(AccountBlockOutcome.AdminBanned,
            AccountBlockGate.Evaluate(true, false, NowUtc.AddMinutes(1), NowUtc));
    }
}
