namespace Fenrir.Data.Abstractions.Admin;

public interface ITribeFourQuotaRepository
{

        public ValueTask<bool> TryConsumeAsync(CancellationToken ct);
}
