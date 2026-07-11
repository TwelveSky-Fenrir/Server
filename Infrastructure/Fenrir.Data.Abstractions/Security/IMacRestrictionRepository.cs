namespace Fenrir.Data.Abstractions.Security;

public interface IMacRestrictionRepository
{

        public ValueTask<bool> IsBannedAsync(string macAddress, string? machineGuid, CancellationToken ct);
}
