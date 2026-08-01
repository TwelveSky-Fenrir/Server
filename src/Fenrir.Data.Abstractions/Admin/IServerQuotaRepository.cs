namespace Fenrir.Data.Abstractions.Admin;

public interface IServerQuotaRepository
{
    public ValueTask<ServerQuotaDto> GetAsync(CancellationToken ct);
}
