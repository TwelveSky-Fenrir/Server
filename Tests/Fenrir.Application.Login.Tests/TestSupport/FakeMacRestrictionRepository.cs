using Fenrir.Data.Abstractions.Security;

namespace Fenrir.Application.Login.Tests.TestSupport;

internal sealed class FakeMacRestrictionRepository(
    string[]? bannedMacAddresses = null,
    IReadOnlyDictionary<string, int>? configuredAccountLimits = null) : IMacRestrictionRepository
{
    private readonly string[] _bannedMacAddresses = bannedMacAddresses ?? [];

    public ValueTask<bool> IsBannedAsync(string macAddress, string? machineGuid, CancellationToken ct)
    {
        return ValueTask.FromResult(_bannedMacAddresses.Contains(macAddress));
    }

    public ValueTask<int?> GetConfiguredAccountLimitAsync(string macAddress, string? machineGuid, CancellationToken ct)
    {
        if (_bannedMacAddresses.Contains(macAddress))
            return ValueTask.FromResult<int?>(0);

        if (configuredAccountLimits is not null && configuredAccountLimits.TryGetValue(macAddress, out var limit))
            return ValueTask.FromResult<int?>(limit);

        return ValueTask.FromResult<int?>(null);
    }

    public ValueTask<int> AddAsync(string macAddress, string? machineGuid, int accountLimit, CancellationToken ct)
    {
        throw new NotSupportedException();
    }
}
