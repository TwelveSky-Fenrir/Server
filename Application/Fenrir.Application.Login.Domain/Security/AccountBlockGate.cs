namespace Fenrir.Application.Login.Domain.Security;

public enum AccountBlockOutcome
{
    Allowed,
    AdminBanned,
    AutoLockedOut
}

public static class AccountBlockGate
{
    public static AccountBlockOutcome Evaluate(bool isBanned, bool hasActiveBanLogEntry, DateTime? lockoutUntilUtc,
        DateTime nowUtc)
    {
        if (isBanned || hasActiveBanLogEntry)
            return AccountBlockOutcome.AdminBanned;

        return lockoutUntilUtc > nowUtc ? AccountBlockOutcome.AutoLockedOut : AccountBlockOutcome.Allowed;
    }
}
