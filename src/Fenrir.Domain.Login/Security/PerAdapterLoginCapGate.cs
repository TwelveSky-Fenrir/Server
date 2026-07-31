namespace Fenrir.Domain.Login.Security;

public enum PerAdapterLoginCapOutcome
{
    Allowed,
    OutrightBanned,
    ConnectionLimitExceeded
}

public static class PerAdapterLoginCapGate
{
    private const int DeadCodeLiveCountBaseline = 1;
    private const int ImplicitDefaultLimit = 2;

    public static PerAdapterLoginCapOutcome Evaluate(int accountGrade, string macAddress,
        int? configuredAccountLimit, int concurrentDeviceSessionCount = 0)
    {
        if (accountGrade >= DeviceSpoofingGuard.GmGradeThreshold || macAddress.Length == 0)
            return PerAdapterLoginCapOutcome.Allowed;

        return configuredAccountLimit switch
        {
            null => concurrentDeviceSessionCount >= ImplicitDefaultLimit
                ? PerAdapterLoginCapOutcome.ConnectionLimitExceeded
                : PerAdapterLoginCapOutcome.Allowed,
            < 1 => PerAdapterLoginCapOutcome.OutrightBanned,
            <= DeadCodeLiveCountBaseline => PerAdapterLoginCapOutcome.ConnectionLimitExceeded,
            _ => PerAdapterLoginCapOutcome.Allowed
        };
    }
}
