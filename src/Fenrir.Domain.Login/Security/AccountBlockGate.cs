namespace Fenrir.Domain.Login.Security;

public enum AccountBlockOutcome
{
    Allowed,
    AdminBanned,
    AutoLockedOut
}

public static class AccountBlockGate
{
    public const int SourceAccountFailureThreshold = 10;

    public const int SourceTotalFailureThreshold = 30;

    public const int AccountFailureThreshold = 20;

    public const int ContestedDistinctOtherSourceThreshold = 2;

    public const int ContestedSourceFailureThreshold = 3;

    public const double BaseRetryDelaySeconds = 30d;

    public const double MaxRetryDelaySeconds = 480d;

    private const int MaxRetryBackoffDoublings = 4;

    public static AccountBlockOutcome EvaluateThrottle(int sourceAccountFailureCount, int sourceTotalFailureCount,
        int accountFailureCount, int distinctOtherSourceCount, DateTime? failureWindowEndsUtc, DateTime nowUtc)
    {
        if (sourceAccountFailureCount >= SourceAccountFailureThreshold ||
            sourceTotalFailureCount >= SourceTotalFailureThreshold)
            return AccountBlockOutcome.AutoLockedOut;

        var contested = failureWindowEndsUtc > nowUtc && accountFailureCount >= AccountFailureThreshold &&
                        distinctOtherSourceCount >= ContestedDistinctOtherSourceThreshold;

        return contested && sourceAccountFailureCount >= ContestedSourceFailureThreshold
            ? AccountBlockOutcome.AutoLockedOut
            : AccountBlockOutcome.Allowed;
    }

    public static double RetryDelaySeconds(int sourceAccountFailureCount, int sourceTotalFailureCount)
    {
        var overage = Math.Clamp(Math.Max(sourceAccountFailureCount - SourceAccountFailureThreshold,
            sourceTotalFailureCount - SourceTotalFailureThreshold), 0, MaxRetryBackoffDoublings);

        return Math.Min(BaseRetryDelaySeconds * (1 << overage), MaxRetryDelaySeconds);
    }

    public static AccountBlockOutcome EvaluateAdminBan(bool isBanned, bool hasActiveBanLogEntry)
    {
        return isBanned || hasActiveBanLogEntry ? AccountBlockOutcome.AdminBanned : AccountBlockOutcome.Allowed;
    }
}
