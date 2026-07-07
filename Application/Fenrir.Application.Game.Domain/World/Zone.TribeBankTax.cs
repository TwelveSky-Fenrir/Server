using System.Collections.Concurrent;
using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     Tribe bank automatic income tax sweep (1% NPC-service tax, 9% monster-kill-currency tax, periodic
///     zone-to-persistent-store flush) -- one <see cref="WorldState.TribeBankTaxAccumulator" /> owned per hosted
///     zone, exactly mirroring the legacy's one-process-per-map <c>mTribeBankInfo[MAX_TRIBE_NUM]</c>.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:3054-3080,2610-2620 ; Server/ts25zone/H07_MyGame.h:108-109 --
///     see <see cref="WorldState.TribeBankTaxAccumulator" />'s own remarks for the full citation set.
///     <para>
///         <b>
///             Neither <see cref="CreditMonsterKillTribeTax" /> nor <see cref="CreditNpcServiceTribeTax" /> is
///             currently called from anywhere in this cluster.
///         </b>
///         <see cref="CreditMonsterKillTribeTax" /> is
///         deliberately NOT wired to <see cref="Monsters.MonsterSpawnScheduler" />'s money-grant hook: in
///         production <c>ts25zone</c>, <c>AddTribeBankInfo3</c> (the 9% monster-kill-currency tax this method
///         models) has exactly one live call site, <c>ProcessForDropItem</c>'s <c>DP_MN_TO_WD</c>/currency-item
///         branch (<c>Server/ts25zone/S07_MyGame03.cpp:486-494</c>), and the live <c>DROP_MONEY</c> block that
///         actually grants monster-kill silver never calls <c>ProcessForDropItem</c> for its own currency
///         grant -- the one line that would have (<c>Server/ts25zone/S07_MyGame05.cpp:2689</c>) is commented
///         out, not live code. So crediting this tax from a monster-kill money grant would manufacture
///         tribe-bank income the legacy production build never actually generates from that source; the method
///         is kept (and directly unit-tested) only because whether the underlying 9% mechanism has some other,
///         not-yet-identified live caller elsewhere in <c>ts25zone</c> is unconfirmed -- see
///         <see cref="WorldState.TribeBankTaxAccumulator" />'s own remarks and the
///         <c>legacy-behavior-translator</c> contract this disconnection was made from before assuming the
///         mechanism can be removed outright. <see cref="CreditNpcServiceTribeTax" /> is separately unwired for
///         an unrelated reason: its eight legacy call sites (item enchant/upgrade, enchant-costume, costume
///         material swap-enchant, item exchange, high-item upgrade, low-item downgrade) all live in
///         <c>Fenrir.Application.Game.Services.ItemModification</c>, outside this cluster's own Domain/World +
///         Hosting scope and reported as touched by a concurrent Commerce-adjacent workflow -- the method
///         exists so that layer can call it once free to do so, and unlike <see cref="CreditMonsterKillTribeTax" />
///         it IS a genuinely live legacy mechanism once wired.
///     </para>
/// </remarks>
public sealed partial class Zone
{
    private readonly ConcurrentQueue<TribeBankTaxSweepPayload> _pendingTribeBankTaxSweeps = new();

    private readonly TribeBankTaxAccumulator _tribeBankTax = tribeBankTax ?? new TribeBankTaxAccumulator();

    /// <summary>Current zone-local running total for one tribe. Test/inspection only.</summary>
    public long GetTribeBankTaxTotal(byte tribeId)
    {
        return _tribeBankTax.GetTotal(tribeId);
    }

    /// <summary>
    ///     9% monster-kill-currency-item tax model. Deliberately NOT called by
    ///     <see cref="Monsters.MonsterSpawnScheduler" />'s <see cref="QueueMoneyGrant" /> money-grant hook --
    ///     see this file's own class remarks for why that specific wiring would be legacy-inaccurate. Kept and
    ///     directly unit-tested as a standalone mechanism pending confirmation of whether it has any other
    ///     legitimate live caller.
    /// </summary>
    public void CreditMonsterKillTribeTax(byte killerTribe, long postReductionAmount)
    {
        _tribeBankTax.CreditMonsterKillCurrencyTax(killerTribe, postReductionAmount);
    }

    /// <summary>1% NPC item-improvement-service tax -- see this file's own remarks for why nothing calls this yet.</summary>
    public void CreditNpcServiceTribeTax(byte payingTribe, int cost)
    {
        _tribeBankTax.CreditNpcServiceTax(payingTribe, cost);
    }

    /// <summary>
    ///     Tick-owned: checked once per <see cref="Tick" />. On the 10-zone-uptime-minute cadence, immediately
    ///     resets the zone-local accumulator (in-memory only, no I/O -- keeps <see cref="Tick" /> fully
    ///     synchronous) and queues the captured payload for <c>TribeBankTaxSweepFlushHost</c> to actually send.
    /// </summary>
    private void TryFlushTribeBankTax()
    {
        if (_tribeBankTax.TrySweep(_clock, out var payload))
            _pendingTribeBankTaxSweeps.Enqueue(payload);
    }

    /// <summary>Callable from any thread; the only intended caller is the background sweep-flush host.</summary>
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
