namespace Fenrir.Data.Abstractions.Admin;

public interface IServerQuotaRepository
{
    public ValueTask<int> GetMaxPlayersAsync(CancellationToken ct);
}
