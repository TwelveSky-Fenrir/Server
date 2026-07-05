using Fenrir.Data.Security;

namespace Fenrir.Application.Login.Tests.TestSupport;

// In-memory stand-in for IMacRestrictionRepository: bans exactly the MAC addresses seeded at construction,
// regardless of MachineGuid -- LoginHandlerTests only need a yes/no answer per MAC.
internal sealed class FakeMacRestrictionRepository(params string[] bannedMacAddresses) : IMacRestrictionRepository
{
    public ValueTask<bool> IsBannedAsync(string macAddress, string? machineGuid, CancellationToken ct)
    {
        return ValueTask.FromResult(bannedMacAddresses.Contains(macAddress));
    }
}
