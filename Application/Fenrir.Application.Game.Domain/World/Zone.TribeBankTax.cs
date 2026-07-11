using System.Collections.Concurrent;
using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private readonly ConcurrentQueue<TribeBankTaxSweepPayload> _pendingTribeBankTaxSweeps = new();

    private readonly TribeBankTaxAccumulator _tribeBankTax = tribeBankTax ?? new TribeBankTaxAccumulator();

        public long GetTribeBankTaxTotal(byte tribeId)
    {
        return _tribeBankTax.GetTotal(tribeId);
    }

        public void CreditMonsterKillTribeTax(byte killerTribe, long postReductionAmount)
    {
        _tribeBankTax.CreditMonsterKillCurrencyTax(killerTribe, postReductionAmount);
    }

        public void CreditNpcServiceTribeTax(byte payingTribe, int cost)
    {
        _tribeBankTax.CreditNpcServiceTax(payingTribe, cost);
    }

        private void TryFlushTribeBankTax()
    {
        if (_tribeBankTax.TrySweep(_clock, out var payload))
            _pendingTribeBankTaxSweeps.Enqueue(payload);
    }

        public IReadOnlyList<TribeBankTaxSweepPayload> DrainPendingTribeBankTaxSweeps()
    {
        if (_pendingTribeBankTaxSweeps.IsEmpty)
            return [];

        List<TribeBankTaxSweepPayload>? sweeps = null;
        while (_pendingTribeBankTaxSweeps.TryDequeue(out var sweep))
            (sweeps ??= []).Add(sweep);

        return (IReadOnlyList<TribeBankTaxSweepPayload>?)sweeps ?? [];
    }
}
