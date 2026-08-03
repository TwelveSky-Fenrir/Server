namespace Fenrir.Domain.Login.Security;

public enum AccountBlockOutcome
{
    Allowed,
    AdminBanned,
    AutoLockedOut
}

public static class AccountBlockGate
{
    public const int SourceFailureThreshold = 10;

    public const int AccountFailureThreshold = 10;

    public const int ContestedSourceFailureThreshold = 3;

    // A source that has produced no failures of its own is never denied; that is what stops a third party
    // pinning someone else's account shut.
    public static AccountBlockOutcome EvaluateThrottle(int sourceFailureCount, int accountFailureCount,
        DateTime? failureWindowEndsUtc, DateTime nowUtc)
    {
        if (sourceFailureCount >= SourceFailureThreshold)
            return AccountBlockOutcome.AutoLockedOut;

        var contested = failureWindowEndsUtc > nowUtc && accountFailureCount >= AccountFailureThreshold;

        return contested && sourceFailureCount >= ContestedSourceFailureThreshold
            ? AccountBlockOutcome.AutoLockedOut
            : AccountBlockOutcome.Allowed;
    }

    public static AccountBlockOutcome EvaluateAdminBan(bool isBanned, bool hasActiveBanLogEntry)
    {
        return isBanned || hasActiveBanLogEntry ? AccountBlockOutcome.AdminBanned : AccountBlockOutcome.Allowed;
    }
}
