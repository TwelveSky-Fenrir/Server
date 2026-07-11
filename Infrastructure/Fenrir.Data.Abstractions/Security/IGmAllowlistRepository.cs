namespace Fenrir.Data.Abstractions.Security;

public interface IGmAllowlistRepository
{
    public ValueTask<bool> IsAllowedAsync(string ipAddress, CancellationToken ct);

        public ValueTask<int> AddAsync(string ipAddress, CancellationToken ct);
}
