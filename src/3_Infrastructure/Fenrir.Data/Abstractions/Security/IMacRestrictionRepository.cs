namespace Fenrir.Data.Abstractions.Security;

public interface IMacRestrictionRepository
{
    public ValueTask<bool> IsBannedAsync(string macAddress, string? machineGuid, CancellationToken ct);

    public ValueTask<int?> GetConfiguredAccountLimitAsync(string macAddress, string? machineGuid,
        CancellationToken ct);

    public ValueTask<int> AddAsync(string macAddress, string? machineGuid, int accountLimit, CancellationToken ct);
}
