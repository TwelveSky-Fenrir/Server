using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Where a Zone241 "Den of Rebirth" challenge instance currently sits -- the 3 coarse outputs the
///     translated behavior contract names for this family (S04_MyWork02.cpp:962-986), independent of which of
///     the 5 legacy event codes (411-415) a future caller decides should produce which value (see
///     <see cref="ZoneCenterBroadcastIngestor" />'s own remarks, GAP 1).
/// </summary>
public enum DenOfRebirthChallengeState : byte
{
    Idle = 0,
    ChallengeStarted = 1,
    Ended = 2
}

/// <summary>
///     Process-wide, thread-safe home for the slice of the legacy ts25center WORLD_INFO singleton that
///     <see cref="WorldState.WorldStateService" />'s own remarks explicitly flag as "no backing table yet":
///     the Zone049/Zone175/Zone267/Zone241/Zone335 numbered siege-event state machines, the per-tribe Zone038
///     DTM effect value, and the three tribe bonus-ratio arrays plus the kill-other-tribe bonus value
///     (<c>Server/Header/Protocol/STRUCT.h:577,580-581,607-608,613,618,624-625,636,642,645,647-649</c>,
///     matching
///     <c>
///         WorldInfo.Zone049TypeState/Zone049TypeStateTime/Zone175TypeState/Zone267TypeState/Zone241TypeState/
///         ZoneFFATypeState/Zone038DTMValue/TribeGeneralExperienceUpRatioInfo/TribeItemDropUpRatioInfo/
///         TribeItemDropUpRatioForMyoungInfo/TribeKillOtherTribeAddValueInfo
///     </c>
///     field-for-field).
///     <para>
///         Deliberately NOT persisted: the legacy ts25center never wrote any of these fields to SQL either --
///         they lived only in the one in-memory WORLD_INFO struct, rebuilt from zero on every center process
///         restart. No write-behind host is wired for this class for exactly that reason (see this cluster's
///         own report to the calling workflow); a future feature that DOES need these to survive a restart
///         needs its own schema + repository from a database-engineer workflow first. Zone049's own two
///         arrays are additionally now fanned out cross-shard by <see cref="ZoneCenterBroadcastIngestor" />'s
///         optional <c>IRvrSiegeEventRelayQueue</c> (see that class's own remarks) precisely BECAUSE this
///         in-memory instance is per-process, not per-cluster -- without that relay, a slot mutated on one
///         shard would never become visible on any other live shard's own copy of this class at all.
///     </para>
///     <para>
///         Bounds: Zone049 is 13 tracked slots, no per-tribe dimension (STRUCT.h:577,607-608); Zone175 is a
///         4-instance x 8-slot grid (STRUCT.h:580-581,613); Zone267/Zone038DTM/the three ratio arrays/
///         kill-other-tribe are all per-tribe (<see cref="WorldState.WorldStateService.TribeCount" /> = 4);
///         Zone241 is 20 instances -- the <c>#else</c> branch of STRUCT.h:583-587's macro pair, confirmed
///         the one actually compiled because the guarding symbol is unconditionally defined at
///         Server/Header/Protocol/DEFINE.h:18 (the 14-instance <c>#ifndef</c> branch is dead in every build);
///         Zone335 has no index at all, a single shared scalar.
///     </para>
/// </summary>
public sealed class ZoneCenterSiegeState
{
    /// <summary>4 concurrent Zone175 instances (STRUCT.h:580-581,613).</summary>
    public const int Zone175Instances = 4;

    /// <summary>8 slots per Zone175 instance (STRUCT.h:580-581,613).</summary>
    public const int Zone175Slots = 8;

    /// <summary>13 tracked Zone049 (Regular War) siege-zone slots (STRUCT.h:577,607-608).</summary>
    public const int Zone049Slots = 13;

    /// <summary>
    ///     20 Den of Rebirth (Zone241) instances -- the compiled <c>#else</c> branch, see class remarks.
    /// </summary>
    public const int Zone241Instances = 20;

    private static readonly int TribeCount = WorldStateService.TribeCount;
    private readonly float[] _experienceBonusRatio = new float[TribeCount];
    private readonly float[] _itemDropBonusRatio = new float[TribeCount];

    private readonly int[] _killOtherTribeBonus = new int[TribeCount];
    private readonly Lock _lock = new();
    private readonly float[] _myoungItemDropBonusRatio = new float[TribeCount];
    private readonly int[] _zone038DtmValue = new int[TribeCount];
    private readonly int[,] _zone175 = new int[Zone175Instances, Zone175Slots];
    private readonly DenOfRebirthChallengeState[] _zone241 = new DenOfRebirthChallengeState[Zone241Instances];
    private readonly int[] _zone267 = new int[TribeCount];
    private readonly int[] _zone049State = new int[Zone049Slots];
    private readonly int[] _zone049StateTime = new int[Zone049Slots];
    private int _zone335;

    // ---- Free-for-all zone / Zone335 (6 events -- 1 no-op -- cases within 1501-1507, reset 1520;
    // S04_MyWork02.cpp:1132-1150) -- a single shared scalar, no index.

    public int Zone335
    {
        get
        {
            lock (_lock)
            {
                return _zone335;
            }
        }
    }

    public static bool IsValidZone175Cell(int instance, int slot)
    {
        return instance is >= 0 and < Zone175Instances && slot is >= 0 and < Zone175Slots;
    }

    public static bool IsValidTribe(int tribeId)
    {
        return tribeId is >= 0 && tribeId < TribeCount;
    }

    public static bool IsValidZone241Instance(int instance)
    {
        return instance is >= 0 and < Zone241Instances;
    }

    public static bool IsValidZone049Slot(int slot)
    {
        return slot is >= 0 and < Zone049Slots;
    }

    // ---- Zone049 / Regular War siege-zone-slot state (sub-codes 1-9, S04_MyWork02.cpp:212-253) ----
    // Sub-code 1 carries no state mutation (its remaining-seconds field only fed the Discord/diagnostic
    // announcement, see ZoneCenterBroadcastIngestor's own remarks) -- there is deliberately no "sub-code 1"
    // write method here. Sub-code 3's redundant, idempotent second write of state 2 plus its two diagnostic
    // log lines (case 1224-1265's secondary switch) is likewise not reproduced: it is real, observable
    // behavior in production per the source contract, but the observable effect is nothing beyond writing the
    // SAME value a second time and emitting server-side log lines with no player-visible or state-visible
    // consequence -- reproducing it here would add a call with zero externally observable difference.

    public int GetZone049State(int slot)
    {
        ValidateZone049Slot(slot);
        lock (_lock)
        {
            return _zone049State[slot];
        }
    }

    public int GetZone049StateTime(int slot)
    {
        ValidateZone049Slot(slot);
        lock (_lock)
        {
            return _zone049StateTime[slot];
        }
    }

    /// <summary>
    ///     Sub-codes 2-9's shared write shape: an unconditional overwrite of <paramref name="slot" />'s state,
    ///     with the state-change timestamp stamped to "now" only when <paramref name="stampTime" /> is true
    ///     (sub-codes 5/6/7/8 stamp; 2/3/4/9 do not) -- see <see cref="ZoneCenterBroadcastIngestor" /> for the
    ///     sub-code-to-(state,stampTime) dispatch table.
    /// </summary>
    public void SetZone049State(int slot, int state, bool stampTime)
    {
        ValidateZone049Slot(slot);
        lock (_lock)
        {
            _zone049State[slot] = state;
            if (stampTime)
                _zone049StateTime[slot] = NowAsLegacyHhMm();
        }
    }

    // ---- Zone175 (37 events, cases 64-100, S04_MyWork02.cpp:613-796; reset case 110, :798-802) ----

    public int GetZone175(int instance, int slot)
    {
        ValidateZone175Cell(instance, slot);
        lock (_lock)
        {
            return _zone175[instance, slot];
        }
    }

    /// <summary>
    ///     GAP: <paramref name="stateCode" /> is whatever the caller supplies -- this cluster's contract only
    ///     gives the AGGREGATE fact that ~15 of the 37 legacy codes collapse onto one shared "generic" state
    ///     constant while ~22 produce a distinct one, not the individual per-code binding, so this class makes
    ///     no attempt to reproduce the literal legacy constant. See <see cref="ZoneCenterBroadcastIngestor" />'s
    ///     own remarks for what it stores here today (the raw event code itself, a placeholder).
    /// </summary>
    public void SetZone175(int instance, int slot, int stateCode)
    {
        ValidateZone175Cell(instance, slot);
        lock (_lock)
        {
            _zone175[instance, slot] = stateCode;
        }
    }

    /// <summary>The separate reset event (case 110): returns one cell to its idle/closed value (0).</summary>
    public void ResetZone175(int instance, int slot)
    {
        ValidateZone175Cell(instance, slot);
        lock (_lock)
        {
            _zone175[instance, slot] = 0;
        }
    }

    // ---- Zone267 (7 events -- 1 no-op -- cases within 402-410, S04_MyWork02.cpp:929-961) ----
    // Despite the legacy field being named as a "zone index," the backing storage is dimensioned by tribe
    // count, not by zone-instance count -- this state is effectively per tribe (STRUCT.h:624).

    public int GetZone267(byte tribeId)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            return _zone267[tribeId];
        }
    }

    public void SetZone267(byte tribeId, int stateCode)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            _zone267[tribeId] = stateCode;
        }
    }

    public void ResetZone267(byte tribeId)
    {
        SetZone267(tribeId, 0);
    }

    // ---- Den of Rebirth / Zone241 (5 events, cases 411-415, S04_MyWork02.cpp:962-986) ----

    public DenOfRebirthChallengeState GetZone241(int instance)
    {
        ValidateZone241Instance(instance);
        lock (_lock)
        {
            return _zone241[instance];
        }
    }

    public void SetZone241(int instance, DenOfRebirthChallengeState state)
    {
        ValidateZone241Instance(instance);
        lock (_lock)
        {
            _zone241[instance] = state;
        }
    }

    public void ResetZone241(int instance)
    {
        SetZone241(instance, DenOfRebirthChallengeState.Idle);
    }

    public void SetZone335(int phaseCode)
    {
        lock (_lock)
        {
            _zone335 = phaseCode;
        }
    }

    public void ResetZone335()
    {
        SetZone335(0);
    }

    /// <summary>
    ///     Zeroes the three tribe bonus-ratio arrays (<see cref="_experienceBonusRatio" />/
    ///     <see cref="_itemDropBonusRatio" />/<see cref="_myoungItemDropBonusRatio" />) plus
    ///     <see cref="_killOtherTribeBonus" />, across all four tribes in one write -- the "four unrelated
    ///     per-tribe world-info bonus fields" the FFA-335 tick's own idle-phase-exit resets alongside its
    ///     (unrelated) kill-tracking-array clear (<c>Server/ts25zone/S07_MyGame01.cpp:10769-10779</c>). NOT
    ///     FFA-specific despite being called from <see cref="Zone335FfaEventCycleSystem" /> -- these four fields
    ///     just happen to be incidentally cleared at that same transition in the legacy source.
    /// </summary>
    public void ResetTribeBonusFields()
    {
        lock (_lock)
        {
            Array.Clear(_experienceBonusRatio);
            Array.Clear(_itemDropBonusRatio);
            Array.Clear(_myoungItemDropBonusRatio);
            Array.Clear(_killOtherTribeBonus);
        }
    }

    // ---- Per-tribe DTM effect value (case 1510, S04_MyWork02.cpp:1160-1164) -- fully precise: reads a
    // tribe number and a whole-number effect value straight from the payload, no per-code guessing needed.

    public int GetZone038DtmValue(byte tribeId)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            return _zone038DtmValue[tribeId];
        }
    }

    public void SetZone038DtmValue(byte tribeId, int value)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            _zone038DtmValue[tribeId] = value;
        }
    }

    // ---- Tribe-wide bonus-ratio sub-selector (S04_MyWork02.cpp:854-919) -- fully precise per field; the
    // event code itself and its payload's sub-selector encoding are NOT given by this cluster's contract (see
    // ZoneCenterBroadcastIngestor's own remarks, GAP 2), so these are plain directly-callable setters, not
    // wired to any dispatch entry yet.

    public float GetExperienceBonusRatio(byte tribeId)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            return _experienceBonusRatio[tribeId];
        }
    }

    public void SetExperienceBonusRatio(byte tribeId, float value)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            _experienceBonusRatio[tribeId] = value;
        }
    }

    public float GetItemDropBonusRatio(byte tribeId)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            return _itemDropBonusRatio[tribeId];
        }
    }

    public void SetItemDropBonusRatio(byte tribeId, float value)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            _itemDropBonusRatio[tribeId] = value;
        }
    }

    public float GetMyoungItemDropBonusRatio(byte tribeId)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            return _myoungItemDropBonusRatio[tribeId];
        }
    }

    public void SetMyoungItemDropBonusRatio(byte tribeId, float value)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            _myoungItemDropBonusRatio[tribeId] = value;
        }
    }

    public int GetKillOtherTribeBonus(byte tribeId)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            return _killOtherTribeBonus[tribeId];
        }
    }

    /// <summary>
    ///     "the kill-other-tribe variant additionally zeroes the same value for the other three tribes in the
    ///     same write" -- precisely given, precisely reproduced: one write, all four slots touched atomically
    ///     under the same lock every reader uses.
    /// </summary>
    public void SetKillOtherTribeBonus(byte tribeId, int value)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            for (byte i = 0; i < TribeCount; i++)
                _killOtherTribeBonus[i] = i == tribeId ? value : 0;
        }
    }

    private static void ValidateZone175Cell(int instance, int slot)
    {
        if (!IsValidZone175Cell(instance, slot))
            throw new ArgumentOutOfRangeException(nameof(instance),
                $"Zone175 instance must be 0-{Zone175Instances - 1} and slot 0-{Zone175Slots - 1}; got instance={instance}, slot={slot}.");
    }

    private static void ValidateZone241Instance(int instance)
    {
        if (!IsValidZone241Instance(instance))
            throw new ArgumentOutOfRangeException(nameof(instance), instance,
                $"Zone241 instance must be 0-{Zone241Instances - 1}.");
    }

    private static void ValidateTribeId(byte tribeId)
    {
        if (!IsValidTribe(tribeId))
            throw new ArgumentOutOfRangeException(nameof(tribeId), tribeId, $"TribeId must be 0-{TribeCount - 1}.");
    }

    private static void ValidateZone049Slot(int slot)
    {
        if (!IsValidZone049Slot(slot))
            throw new ArgumentOutOfRangeException(nameof(slot), slot, $"Zone049 slot must be 0-{Zone049Slots - 1}.");
    }

    /// <summary>
    ///     Mirrors the legacy's <c>ReturnNowTime()</c> (datetime.h:85-90): hour*100+minute, display-only -- same
    ///     convention <see cref="WorldState.WorldStateService" />'s own private helper of the same name uses for
    ///     Zone038WinTribeTime/MonsterSymbolEndTime, citing the identical "use realtime" zone-side comment
    ///     (S07_MyGame08.cpp:205,292) this class's own Zone049 remarks cite too.
    /// </summary>
    private static int NowAsLegacyHhMm()
    {
        var now = DateTime.UtcNow;
        return now.Hour * 100 + now.Minute;
    }
}
