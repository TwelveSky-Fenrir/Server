namespace Fenrir.Domain.Login.Security;

public static class DeviceSpoofingGuard
{
    private const string PlaceholderMacAddress = "00-00-00-00-00-00";

    public const int GmGradeThreshold = 1;
    public const string PlaceholderAdapterGuid = "{0-0-0-0-0}";
    public const string PlaceholderRemoteIp = "127.0.0.1";

    public static bool IsSpoofedDeviceTuple(int accountGrade, string declaredMacAddress,
        string declaredAdapterGuid, string? observedRemoteIp)
    {
        if (accountGrade >= GmGradeThreshold)
            return false;

        if (declaredMacAddress.Length == 0)
            return true;

        return declaredMacAddress == PlaceholderMacAddress
               || declaredAdapterGuid == PlaceholderAdapterGuid
               || observedRemoteIp == PlaceholderRemoteIp;
    }
}
