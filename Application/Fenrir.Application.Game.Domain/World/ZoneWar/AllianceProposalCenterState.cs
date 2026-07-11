using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     One tribe's "possible new alliance" cooldown marker -- <c>mPossibleAllianceInfo[tribeId]</c>'s two cells,
///     a cooldown-active flag plus the date it lifts, formatted as an 8-digit year-month-day integer (e.g.
///     20260724) exactly as the wire payload carries it (<c>Server/ts25center/S04_MyWork02.cpp:430-431,453-454</c>).
///     This type performs no date-format decoding of its own -- the contract describes the wire shape only, not a
///     packing formula, so the value is stored and returned opaque.
/// </summary>
public readonly record struct AlliancePossibleInfo(bool CooldownActive, int ExpiryDateYyyyMmDd)
{
    public static readonly AlliancePossibleInfo Cleared = new(false, 0);
}

/// <summary>
///     Process-wide, thread-safe home for the two WORLD_INFO alliance fields <c>WorldStateService</c>'s own
///     remarks explicitly flag as "not modeled by this schema yet" (see that class's <c>SetAllianceOffer</c>
///     remarks): <c>mAllianceState[2][2]</c> (two alliance-proposal slots, each holding a tribe-id pair or empty)
///     and <c>mPossibleAllianceInfo[MAX_TRIBE_NUM][2]</c> (one <see cref="AlliancePossibleInfo" /> per tribe).
///     <para>
///         Distinct from, and not yet reconciled with, <c>WorldStateService</c>'s own
///         <c>_allianceOffers</c>/<c>SetAllianceOffer</c>/<c>GetAllyOf</c>/<c>DissolveAlliance</c> family (a
///         simpler directed (from,to,isAccepted) offer dictionary) and <c>AllianceCooldownTracker</c> (a
///         per-tribe <c>DateOnly</c> cooldown already written by <c>AllianceDiplomacyCeremony</c>'s own
///         already-allied-negotiation completion). Those two existing types model the OUTCOME an alliance
///         change has on gameplay-facing lookups; THIS type models the literal two-slot/per-tribe-marker STORAGE
///         SHAPE the center-side dispatch discriminators (46-49) read and write, byte-for-byte, including the
///         legacy's own unverified slot-1 fallback (see <see cref="SlotMatchesPairEitherOrder" />'s remarks).
///         Reconciling the two representations (e.g. having the ritual's break-path also/instead route through
///         this type) is an explicit open design decision for a future integration pass, not resolved here --
///         see this slice's own reported wiring manifest.
///     </para>
///     <para>
///         Empty-slot sentinel: the legacy source was not cited with the literal sentinel value <c>mAllianceState</c>
///         cells use for "no tribe here" (commonly <c>-1</c> in this codebase's C++ arrays elsewhere, but not
///         confirmed for this specific field by the translating contract) -- rather than invent that magic
///         number, this type models an empty cell as <c>byte?</c> <see langword="null" />, which is functionally
///         identical to whatever sentinel legacy actually uses (a value distinguishable from every valid 0-3
///         tribe id) without guessing its literal bit pattern.
///     </para>
///     <para>
///         Bounds: unlike the legacy dispatch handler itself (Réf. C++ below -- no range check on either tribe-id
///         input anywhere in the cited cases), every mutator here validates <c>tribeId</c>/<c>slot</c> against
///         <see cref="WorldStateService.TribeCount" />/<see cref="SlotCount" /> and throws --
///         matching the same Fenrir-side hardening precedent <c>ZoneCenterSiegeState</c>'s own family of
///         <c>IsValidXxx</c> guards already applies to every other numbered siege-state family sharing this same
///         wire message.
///     </para>
///     <para>
///         Not persisted: like <c>ZoneCenterSiegeState</c> (see that class's own remarks), this schema has no
///         backing SQL column for either field yet -- <c>game.WorldStateAllianceOffers</c> only covers the
///         simpler offer-dictionary shape above. A durable version needs new columns plus
///         <c>IWorldStateRepository</c>/stored-procedure support from a <c>fenrir-database-engineer</c>
///         follow-up; this instance is this ONE process's own in-memory copy only, reset on every restart.
///     </para>
/// </summary>
/// <remarks>
///     Réf. C++ : Server/Header/Protocol/STRUCT.h:590-601 (<c>WORLD_INFO</c>: <c>mPossibleAllianceInfo[MAX_TRIBE_NUM][2]</c>,
///     <c>mAllianceState[2][2]</c>) ; Server/Header/Protocol/DEFINE.h:309 (<c>MAX_TRIBE_NUM = 4</c>) ;
///     Server/ts25center/S04_MyWork02.cpp:408-426 (case 46 slot-selection/clear/write -- unreachable, see
///     <see cref="AllianceProposalCenterEventMap" />'s own remarks) ; :427-448 (case 47, identical slot-selection
///     shape to case 46 but matching an existing pair rather than testing emptiness) ; :450-472 (case 49, byte-for-byte
///     identical mutation logic to case 47).
/// </remarks>
public sealed class AllianceProposalCenterState
{
    /// <summary><c>mAllianceState</c>'s outer dimension -- exactly two proposal slots (STRUCT.h:601).</summary>
    public const int SlotCount = 2;

    private static readonly int TribeCount = WorldStateService.TribeCount;

    private readonly byte?[,] _allianceState = new byte?[SlotCount, 2];
    private readonly AlliancePossibleInfo[] _possibleAllianceInfo = new AlliancePossibleInfo[TribeCount];
    private readonly Lock _lock = new();

    public static bool IsValidSlot(int slot)
    {
        return slot is >= 0 and < SlotCount;
    }

    public static bool IsValidTribe(int tribeId)
    {
        return tribeId >= 0 && tribeId < TribeCount;
    }

    /// <summary>The two tribe ids currently occupying <paramref name="slot" />'s two cells, or null per empty cell.</summary>
    public (byte? CellA, byte? CellB) GetSlot(int slot)
    {
        ValidateSlot(slot);
        lock (_lock)
        {
            return (_allianceState[slot, 0], _allianceState[slot, 1]);
        }
    }

    /// <summary>Unconditional overwrite of both of <paramref name="slot" />'s cells -- no merge, no prior-value check.</summary>
    public void SetSlot(int slot, byte? cellA, byte? cellB)
    {
        ValidateSlot(slot);
        lock (_lock)
        {
            _allianceState[slot, 0] = cellA;
            _allianceState[slot, 1] = cellB;
        }
    }

    /// <summary>Returns <paramref name="slot" />'s two cells to the empty sentinel (both null).</summary>
    public void ClearSlot(int slot)
    {
        SetSlot(slot, null, null);
    }

    /// <summary>True if both of <paramref name="slot" />'s cells are the empty sentinel.</summary>
    public bool SlotIsEmpty(int slot)
    {
        ValidateSlot(slot);
        lock (_lock)
        {
            return SlotIsEmptyCore(slot);
        }
    }

    /// <summary>
    ///     True if <paramref name="slot" />'s two cells hold exactly <paramref name="tribeA" />/<paramref name="tribeB" />
    ///     in either order -- the "checked both orderings" match cases 47/49 perform against slot 0 before
    ///     falling back to slot 1 unconditionally (S04_MyWork02.cpp:432-440,455-463). This method itself makes no
    ///     claim about slot 1 -- it only answers the question for whichever slot it is called with; the "no
    ///     independent verification of slot 1" behavior lives in <see cref="ApplyBreak" />, matching the legacy
    ///     dispatch handler's own shape exactly, unreconciled defect included.
    /// </summary>
    public bool SlotMatchesPairEitherOrder(int slot, byte tribeA, byte tribeB)
    {
        ValidateSlot(slot);
        lock (_lock)
        {
            return SlotMatchesPairEitherOrderCore(slot, tribeA, tribeB);
        }
    }

    /// <summary>Current cooldown marker for <paramref name="tribeId" /> -- <see cref="AlliancePossibleInfo.Cleared" /> if never set.</summary>
    public AlliancePossibleInfo GetPossibleAllianceInfo(byte tribeId)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            return _possibleAllianceInfo[tribeId];
        }
    }

    /// <summary>
    ///     Arms <paramref name="tribeId" />'s cooldown marker: active flag on, expiry set to
    ///     <paramref name="expiryDateYyyyMmDd" /> (cases 47/49's shared shape, S04_MyWork02.cpp:441-444,465-468).
    ///     Exposed as a standalone single-field setter for direct testing/composition; production dispatch goes
    ///     through <see cref="ApplyBreak" /> instead so the whole multi-field transition lands under one lock --
    ///     see that method's own remarks.
    /// </summary>
    public void SetPossibleAllianceCooldown(byte tribeId, int expiryDateYyyyMmDd)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            _possibleAllianceInfo[tribeId] = new AlliancePossibleInfo(true, expiryDateYyyyMmDd);
        }
    }

    /// <summary>
    ///     Case 46's own shape: zeroes both of <paramref name="tribeId" />'s marker cells outright
    ///     (S04_MyWork02.cpp:419-422) -- unlike <see cref="SetPossibleAllianceCooldown" />, this does not arm the
    ///     cooldown, it clears it. Same single-field-setter caveat as that method -- production dispatch goes
    ///     through <see cref="ApplyFinalize" /> instead.
    /// </summary>
    public void ClearPossibleAllianceCooldown(byte tribeId)
    {
        ValidateTribeId(tribeId);
        lock (_lock)
        {
            _possibleAllianceInfo[tribeId] = AlliancePossibleInfo.Cleared;
        }
    }

    /// <summary>
    ///     Case 46's ENTIRE mutation (S04_MyWork02.cpp:408-426) as one atomic unit under a single lock acquisition
    ///     -- slot selection (slot 0 if currently empty, else slot 1 unconditionally), both tribes' cooldown
    ///     markers cleared, the pair written into the chosen slot. Bundled into one lock (rather than the three
    ///     separate single-field setters above called in sequence) so a concurrent reader can never observe a
    ///     torn intermediate state (e.g. cooldowns already cleared but the slot not yet written) -- the same
    ///     "one write, all fields touched atomically under the same lock every reader uses" precedent
    ///     <see cref="ZoneCenterSiegeState.SetKillOtherTribeBonus" /> already establishes for a comparable
    ///     multi-field write elsewhere in this same dispatch surface. This is the entry point
    ///     <see cref="AllianceProposalCenterEventMap" /> actually calls; the single-field setters above remain
    ///     public for direct unit testing and any future caller that genuinely wants just one field touched.
    /// </summary>
    public void ApplyFinalize(byte tribeA, byte tribeB)
    {
        ValidateTribeId(tribeA);
        ValidateTribeId(tribeB);
        lock (_lock)
        {
            var slot = SlotIsEmptyCore(0) ? 0 : 1;
            _possibleAllianceInfo[tribeA] = AlliancePossibleInfo.Cleared;
            _possibleAllianceInfo[tribeB] = AlliancePossibleInfo.Cleared;
            _allianceState[slot, 0] = tribeA;
            _allianceState[slot, 1] = tribeB;
        }
    }

    /// <summary>
    ///     Cases 47/49's ENTIRE mutation (S04_MyWork02.cpp:427-448,450-472) as one atomic unit under a single
    ///     lock acquisition -- slot selection (slot 0 if it already holds this exact pair in either order, else
    ///     slot 1 unconditionally, with no independent verification of slot 1), both tribes' cooldown markers
    ///     armed to their own sent expiry date, the selected slot cleared back to empty. Same atomicity rationale
    ///     as <see cref="ApplyFinalize" />'s own remarks.
    /// </summary>
    public void ApplyBreak(byte tribeA, byte tribeB, int expiryDateA, int expiryDateB)
    {
        ValidateTribeId(tribeA);
        ValidateTribeId(tribeB);
        lock (_lock)
        {
            var slot = SlotMatchesPairEitherOrderCore(0, tribeA, tribeB) ? 0 : 1;
            _possibleAllianceInfo[tribeA] = new AlliancePossibleInfo(true, expiryDateA);
            _possibleAllianceInfo[tribeB] = new AlliancePossibleInfo(true, expiryDateB);
            _allianceState[slot, 0] = null;
            _allianceState[slot, 1] = null;
        }
    }

    /// <summary>Must only be called with <see cref="_lock" /> already held.</summary>
    private bool SlotIsEmptyCore(int slot)
    {
        return _allianceState[slot, 0] is null && _allianceState[slot, 1] is null;
    }

    /// <summary>Must only be called with <see cref="_lock" /> already held.</summary>
    private bool SlotMatchesPairEitherOrderCore(int slot, byte tribeA, byte tribeB)
    {
        var cellA = _allianceState[slot, 0];
        var cellB = _allianceState[slot, 1];
        return (cellA == tribeA && cellB == tribeB) || (cellA == tribeB && cellB == tribeA);
    }

    private static void ValidateSlot(int slot)
    {
        if (!IsValidSlot(slot))
            throw new ArgumentOutOfRangeException(nameof(slot), slot, $"Alliance proposal slot must be 0-{SlotCount - 1}.");
    }

    private static void ValidateTribeId(byte tribeId)
    {
        if (!IsValidTribe(tribeId))
            throw new ArgumentOutOfRangeException(nameof(tribeId), tribeId, $"TribeId must be 0-{TribeCount - 1}.");
    }
}
