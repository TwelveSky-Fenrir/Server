namespace Fenrir.Data.Abstractions.Tribes;

public interface ITribeBankSweepRepository
{

        public ValueTask ApplyTaxSweepAsync(long tribe0Amount, long tribe1Amount, long tribe2Amount,
        long tribe3Amount, CancellationToken ct);
}
