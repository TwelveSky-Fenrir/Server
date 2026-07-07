namespace Fenrir.Application.Game.Domain.World.WorldState;

/// <summary>
///     One sweep's four-tribe running totals, ready to be sent to the persistent tribe-bank store in a single
///     message -- the in-memory twin of the legacy <c>mTribeBankInfo[MAX_TRIBE_NUM]</c> array at the instant it
///     is captured and zeroed by <see cref="TribeBankTaxAccumulator.TrySweep" />.
/// </summary>
public readonly record struct TribeBankTaxSweepPayload(long Tribe0, long Tribe1, long Tribe2, long Tribe3)
{
    public static readonly TribeBankTaxSweepPayload Empty = default;

    public bool IsEmpty => Tribe0 == 0 && Tribe1 == 0 && Tribe2 == 0 && Tribe3 == 0;

    /// <summary>
    ///     Indexed accessor for the 4 fixed tribe slots -- throws outside 0-3, same contract as
    ///     <see cref="TribeBankTaxAccumulator.GetTotal" />.
    /// </summary>
    public long this[byte tribeId] => tribeId switch
    {
        0 => Tribe0,
        1 => Tribe1,
        2 => Tribe2,
        3 => Tribe3,
        _ => throw new ArgumentOutOfRangeException(nameof(tribeId), tribeId,
            $"TribeId must be 0-{TribeBankTaxAccumulator.TribeCount - 1}.")
    };
}

/// <summary>
///     Zone-instance-local "tribe bank income tax" accumulator: 1% of every NPC item-improvement service's
///     already-charged cost, and 9% of every monster-kill currency-item drop's already-resolved amount, is
///     credited here -- additional to, never subtracted from, whatever money the paying/killing player
///     themselves keeps. One instance is owned by exactly one hosted <see cref="Zone" />, mirroring the legacy's
///     one-process-per-map <c>mTribeBankInfo[MAX_TRIBE_NUM]</c>/<c>mUpdateTimeForTribeBankInfo</c> pair: every
///     zone's own running totals and its own 10-zone-uptime-minute sweep clock are entirely independent of every
///     other zone's.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:3054-3080 (<c>AddTribeBankInfo</c>/<c>AddTribeBankInfo2</c>/
///     <c>AddTribeBankInfo3</c> -- tribe-symbol indirection, the 2,000,000,000 zone-local overflow-drop cap, the
///     fixed 1%/9% rates, <c>#ifdef LNW33</c> active in the production <c>ReleaseEU33</c> build) ;
///     Server/ts25zone/S07_MyGame01.cpp:2610-2620 (periodic 10-minute sweep condition, fire-and-forget send,
///     unconditional zero-reset of the zone-local accumulator immediately after sending) ;
///     Server/ts25zone/H07_MyGame.h:108-109 (<c>mUpdateTimeForTribeBankInfo</c>/<c>mTribeBankInfo[MAX_TRIBE_NUM]</c>
///     field declarations -- zone-instance-local state) ; Server/Header/Protocol/DEFINE.h:309 (<c>MAX_TRIBE_NUM</c>
///     = 4) ; Server/Header/Protocol/DEFINE.h:365 (<c>MAX_NUMBER_SIZE</c> = 2,000,000,000, the same ceiling used
///     both zone-locally and per persistent slot) ; Server/Header/function.h:132-140 (<c>CheckOverMaximum</c> --
///     blocks the add only when the sum would exceed the ceiling, used identically at both layers).
///     <para>
///         <see cref="TribeBankTaxAccumulator(Func{byte, byte}?)" />'s optional resolver is the tribe-symbol
///         indirection seam (legacy <c>mTribeSymbol[4]</c>, reassignable by the Zone038 tribe-symbol-battle
///         mechanic -- explicitly out of scope for the translated contract this class implements). Fenrir's
///         <see cref="WorldStateService" /> does not yet record "who currently owns slot X" -- only a per-slot
///         bool "did this tribe keep its OWN slot" (<see cref="WorldStateService.ResolveTribeSymbol" />'s own
///         remarks) -- so there is no real reassignment data to read yet. Defaulting to identity keeps this a
///         documented no-op, exactly the contract's own described default state, with the delegate seam ready
///         for whenever slot-ownership data exists.
///     </para>
/// </remarks>
public sealed class TribeBankTaxAccumulator(Func<byte, byte>? resolveBeneficiaryTribe = null)
{
    /// <summary>Server/Header/Protocol/DEFINE.h:309.</summary>
    public const int TribeCount = 4;

    /// <summary>
    ///     Server/Header/Protocol/DEFINE.h:365 -- the same ceiling enforced zone-locally here and, separately, per
    ///     persistent slot.
    /// </summary>
    public const long ZoneLocalCeiling = 2_000_000_000L;

    /// <summary>1%, truncated toward zero -- the NPC item-improvement-service tax rate.</summary>
    public const double NpcServiceTaxRate = 0.01;

    /// <summary>9%, truncated toward zero -- the monster-kill currency-item-drop tax rate.</summary>
    public const double MonsterKillCurrencyTaxRate = 0.09;

    /// <summary>Server/ts25zone/S07_MyGame01.cpp:2610-2620.</summary>
    public static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(10);

    private readonly Lock _lock = new();
    private readonly long[] _totals = new long[TribeCount];
    private TimeSpan _lastSweepAt = TimeSpan.Zero;

    /// <summary>Current zone-local running total for one tribe. Test/inspection only -- never itself mutated.</summary>
    public long GetTotal(byte tribeId)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            return _totals[tribeId];
        }
    }

    /// <summary>
    ///     1% of an already-charged NPC item-improvement-service cost (item enchant/upgrade -- success or
    ///     failure path, both charge up front --, enchant-costume, costume material swap-enchant, item exchange,
    ///     "high item" upgrade, "low item" downgrade). All eight legacy call sites already deducted
    ///     <paramref name="cost" /> from the paying player before this is credited; nothing here ever touches
    ///     the player's own currency.
    /// </summary>
    public void CreditNpcServiceTax(byte payingTribe, int cost)
    {
        Credit(payingTribe, cost, NpcServiceTaxRate);
    }

    /// <summary>
    ///     9% of a monster-kill currency-item drop's already-resolved amount -- after the unrelated 15%
    ///     monster-kill-currency reduction already applied upstream, before any territorial tower-silver-gain
    ///     bonus is added on top of what the killer actually receives. Additional to, never subtracted from, the
    ///     killer's own award.
    /// </summary>
    /// <remarks>
    ///     Not currently invoked from Fenrir's own monster-kill money-grant path
    ///     (<see cref="World.Monsters.MonsterSpawnScheduler.Simulate" />'s death handling): in production
    ///     <c>ts25zone</c>, the live <c>DROP_MONEY</c> block never routes through <c>ProcessForDropItem</c> (the
    ///     one call site of <c>AddTribeBankInfo3</c>, the function this method models) -- see
    ///     <see cref="Zone.CreditMonsterKillTribeTax" />'s own remarks for the full citation set. Kept and
    ///     directly unit-tested as a standalone mechanism pending confirmation of whether the 9% rate has any
    ///     other legitimate live caller elsewhere in <c>ts25zone</c>.
    /// </remarks>
    public void CreditMonsterKillCurrencyTax(byte killerTribe, long postReductionAmount)
    {
        Credit(killerTribe, postReductionAmount, MonsterKillCurrencyTaxRate);
    }

    private void Credit(byte actingTribe, long baseAmount, double rate)
    {
        if (actingTribe >= TribeCount)
            return; // Out-of-range acting tribe: skipped entirely, no error anywhere in the chain.

        var beneficiary = resolveBeneficiaryTribe?.Invoke(actingTribe) ?? actingTribe;
        if (beneficiary >= TribeCount)
            return; // Defensive only -- a well-behaved resolver never returns an out-of-range slot.

        var tax = (long)(baseAmount * rate); // Truncated toward zero, matching the legacy's own int cast.
        if (tax <= 0)
            return;

        lock (_lock)
        {
            if (_totals[beneficiary] + tax > ZoneLocalCeiling)
                return; // Whole addition silently dropped -- no partial credit, no error.

            _totals[beneficiary] += tax;
        }
    }

    /// <summary>
    ///     Due at most once per <see cref="SweepInterval" /> of <paramref name="zoneUptimeNow" /> (10 zone-uptime
    ///     minutes since the previous sweep, or since zone-process start for the very first one -- this class
    ///     has no wall-clock concept, so <paramref name="zoneUptimeNow" /> must be the caller's own monotonic
    ///     "elapsed since this zone started ticking" clock). On a hit, snapshots and unconditionally zeroes every
    ///     tribe's running total in the same step: the reset does not wait on, and is not undone by, whatever the
    ///     caller does with the returned <paramref name="payload" /> afterward -- a lost/failed downstream send
    ///     destroys that window's entire tax for all four tribes with no retry, matching the legacy's own
    ///     fire-and-forget <c>U_ZONE_TRIBE_BANK_SAVE_FOR_PLAYUSER_SEND</c>.
    /// </summary>
    public bool TrySweep(TimeSpan zoneUptimeNow, out TribeBankTaxSweepPayload payload)
    {
        lock (_lock)
        {
            if (zoneUptimeNow - _lastSweepAt < SweepInterval)
            {
                payload = TribeBankTaxSweepPayload.Empty;
                return false;
            }

            payload = new TribeBankTaxSweepPayload(_totals[0], _totals[1], _totals[2], _totals[3]);
            Array.Clear(_totals);
            _lastSweepAt = zoneUptimeNow;
            return true;
        }
    }

    private static void ValidateTribeId(byte tribeId)
    {
        if (tribeId >= TribeCount)
            throw new ArgumentOutOfRangeException(nameof(tribeId), tribeId, $"TribeId must be 0-{TribeCount - 1}.");
    }
}
