namespace Fenrir.Data.Abstractions.Security;

public interface IBlockedIpRepository
{
    public ValueTask<bool> IsBlockedAsync(string ipAddress, CancellationToken ct);
}
