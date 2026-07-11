using Fenrir.Data.Abstractions.Security;

namespace Fenrir.Application.Login.Tests.TestSupport;

internal sealed class FakeMacRestrictionRepository(params string[] bannedMacAddresses) : IMacRestrictionRepository
{
    public ValueTask<bool> IsBannedAsync(string macAddress, string? machineGuid, CancellationToken ct)
    {
        return ValueTask.FromResult(bannedMacAddresses.Contains(macAddress));
    }
}
