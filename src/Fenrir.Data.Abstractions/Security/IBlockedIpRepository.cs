namespace Fenrir.Data.Abstractions.Security;

public interface IBlockedIpRepository
{
    public ValueTask<bool> IsBlockedAsync(string ipAddress, CancellationToken ct);

    public ValueTask<int> AddAsync(string ipAddress, CancellationToken ct);
}
