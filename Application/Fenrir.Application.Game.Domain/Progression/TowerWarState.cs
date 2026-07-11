using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Progression;

/// <summary>Where one of the 12 towers sits in its build/siege lifecycle. Derived, never stored directly -- see remarks.</summary>
public enum TowerSiegePhase : byte
{
    /// <summary>Level 0, no guardian. Reached only by <see cref="TowerWarState.CompleteDestruction" /> or a fresh boot.</summary>
    Dormant,

    /// <summary>
    ///     A target level/type is recorded (fresh upgrade submission, or a just-booted process resuming a
    ///     previously-built tower) but the guardian monster for it has not been (re)spawned into its zone yet.
    /// </summary>
    Building,

    /// <summary>Guardian alive at the tower's current level/type; armed for the next CZ_CHUGSOUNG_WAR_UP_SEND.</summary>
    Active,

    /// <summary>
    ///     The guardian was destroyed; <see cref="TowerWarState.SiegeCollapseCooldown" /> is counting down to
    ///     <see cref="Dormant" />.
    /// </summary>
    Sieged
}

/// <summary>
///     Process-wide mirror of the legacy tower-war state CZ_CHUGSOUNG_WAR_UP_SEND reads/writes. The legacy
///     ran one zone-server process per tower, splitting this across two places: the shared-memory
///     <c>TOWER_INFO.mState1Tower[12]</c> array (packed level*100+type, decoded by
///     <c>MyGame::GetTowerState</c>/<c>GetTowerType</c>) and each zone-server's own local
///     <c>mTowerValid</c>/<c>mTowerState</c> scalars for whichever single tower it owned. Fenrir runs every
///     zone in one process, so both collapse onto one per-tower record here; every state the legacy could
///     reach produces the same decoded level/type/valid values either way.
///     <para>
///         <see cref="TowerSiegePhase" /> is derived, not stored: <c>Sieged</c> iff a siege timestamp is set,
///         else <c>Building</c> iff a pending level/type is queued, else <c>Active</c> iff <see cref="IsValid" />,
///         else <c>Dormant</c>. <see cref="Progression.TowerGuardianSystem" /> reads <see cref="GetPhase" /> once
///         per tick (for the one zone that hosts each tower) to drive the guardian <c>MonsterEntity</c> in and
///         out of that zone's own live pool -- see its own remarks for the full state machine.
///     </para>
///     <para>
///         Backed by game.TowerState/usp_TowerState_* via <see cref="InitializeAsync" /> (boot load) and
///         <see cref="FlushDirtyAsync" /> (periodic write-behind, <see cref="TowerWarWriteBehindHost" />) --
///         only Level/TowerType/ControllingTribeId persist; the in-flight Building/Sieged countdowns are
///         transient and simply restart on a reboot (a live guardian <c>MonsterEntity</c> never survives a
///         process restart either, so there is nothing meaningful to resume mid-cooldown).
///     </para>
///     <para>
///         <see cref="RecomputeTribeBonuses" />/<see cref="GetTribeBonus" /> are a second, independent
///         concern layered on the same packed state: the per-tribe Silver/CP-for-PvM/CP-for-PvP/XP reward
///         bonus table (legacy <c>UpdateTowerBonus</c>), never persisted (purely derived from
///         <see cref="GetPackedState" /> every tick, see <see cref="TowerRewardBonusTable" />).
///     </para>
/// </summary>
public sealed class TowerWarState(ILogger<TowerWarState>? logger = null)
{
    public const int TowerCount = 12;

    /// <summary>
    ///     A11 -- the maximum number of towers of a single kind that may exist cluster-wide (across all 12 slots,
    ///     states 1-8); an item-665 construct is refused as a soft inventory-use failure once this many of the
    ///     requested kind already exist (S04_MyWork03.cpp:7340-7355, contract edge case "more than 3 already
    ///     exist"). The exact comparison boundary (&gt;3 vs &gt;=3) was summarized from the un-opened count loop --
    ///     flagged for cpp-zone-gameplay-analyst re-check; implemented here as "refuse once the existing count of
    ///     the kind exceeds this value".
    /// </summary>
    public const int MaxTowersPerKind = 3;

    /// <summary>
    ///     Legacy <c>destroyCoolDown</c> = 5.0f minutes (<c>GetGameTickMinute(5f)</c> = 600 legacy ticks @ 500 ms
    ///     = 300 s), the wait in legacy state 90 between the guardian dying and the tower fully reverting to 0
    ///     (S07_MyGame01.cpp:13662,13768).
    /// </summary>
    public static readonly TimeSpan SiegeCollapseCooldown = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     A11 -- legacy <c>createCoolDown</c> = 5.0f minutes: the post-create wait in legacy state 1 between a
    ///     freshly-summoned level-1 guardian and the tower becoming a live, attackable "created level-1" tower
    ///     (state 2). Same 5-minute magnitude as <see cref="SiegeCollapseCooldown" /> but a distinct lifecycle
    ///     phase (S07_MyGame01.cpp:13661,13714-13722, contract side effect A.4).
    /// </summary>
    public static readonly TimeSpan CreateCooldown = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     A11 -- the tower-attack AI's ~30-second "no further hits" window after which a guardian's attack-state
    ///     returns to "ready" (S07_MyGame05.cpp:851, contract side effect D). Wall-clock derived, not tick-counted.
    /// </summary>
    public static readonly TimeSpan AttackStateIdleReset = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     A11 -- the tower-attack AI's 10-minute auto-clear of an engagement's one-shot first-attack tracking, so
    ///     a fresh engagement re-arms the one-time Center-754 notification (S07_MyGame05.cpp:857, contract side
    ///     effect D). Wall-clock derived.
    /// </summary>
    public static readonly TimeSpan EngagementAutoClear = TimeSpan.FromMinutes(10);

    /// <summary>
    ///     A11 -- the queued construct kind (1/2/3) for a tower armed by an item-665 use but whose level-1 guardian
    ///     has not been summoned yet (legacy "creating" states 101/102/103). 0 = not constructing. Cleared to 0 by
    ///     <see cref="CompleteConstructionSpawn" /> once the guardian is up (the kind then lives in the packed
    ///     state as its DecodeType). See <see cref="TowerLifecycleSystem" /> for the tick-side advance.
    /// </summary>
    private readonly int[] _constructKind = new int[TowerCount];

    private readonly byte?[] _controllingTribe = new byte?[TowerCount];

    /// <summary>
    ///     A11 -- when the post-create cooldown (legacy state 1) started for a tower whose level-1 guardian is up
    ///     but not yet attackable; null once the tower has advanced to the live "created level-1" state (2).
    /// </summary>
    private readonly DateTime?[] _createCooldownStartedAtUtc = new DateTime?[TowerCount];

    private readonly bool[] _dirty = new bool[TowerCount];

    private readonly DateTime?[] _firstAttackAtUtc = new DateTime?[TowerCount];

    /// <summary>One-shot per guardian lifetime, reset alongside everything else in <see cref="CompleteUpgrade" />.</summary>
    private readonly bool[] _firstAttackRecorded = new bool[TowerCount];

    /// <summary>
    ///     A11 -- an item-667 heal (guardian +10% max life) has been validated and its item consumed on the
    ///     handler thread and is waiting to be applied to the live guardian <c>MonsterEntity</c> on that tower's
    ///     own zone tick (the life mutation + broadcast is a tick-owned handoff -- see this file's own remarks and
    ///     the wiring manifest). Drained by <see cref="TryConsumeGuardianHeal" />.
    /// </summary>
    private readonly bool[] _guardianHealPending = new bool[TowerCount];

    private readonly DateTime?[] _lastAttackAtUtc = new DateTime?[TowerCount];
    private readonly Lock _lock = new();
    private readonly int[] _packedState = new int[TowerCount];
    private readonly int[] _pendingPackedState = new int[TowerCount];
    private readonly byte?[] _pendingTribe = new byte?[TowerCount];
    private readonly DateTime?[] _siegeStartedAtUtc = new DateTime?[TowerCount];

    /// <summary>
    ///     Latest <see cref="TowerRewardBonusTable.Recompute" /> result, one entry per tribe (0-3) --
    ///     refreshed once per tick by <see cref="TowerRewardBonusSystem" />, read by
    ///     <see cref="World.Zone" />'s tower-reward consumption hooks. All-zero until the first recompute.
    /// </summary>
    private readonly TowerTribeRewardBonus[] _tribeBonus = new TowerTribeRewardBonus[TowerRewardBonusTable.TribeCount];

    /// <summary>
    ///     The client-facing "under siege" broadcast flag -- cleared by every allowed, landed guardian hit (see
    ///     <see cref="RecordGuardianHit" />). Nothing in this cluster ever sets it back to true: that's the
    ///     adjacent ~30 s idle-tick timer (S07_MyGame05.cpp:844-863), out of scope here -- see
    ///     <see cref="Progression.TowerFriendlyFireGate" />'s remarks.
    /// </summary>
    private readonly bool[] _underAttack = new bool[TowerCount];

    private readonly bool[] _valid = new bool[TowerCount];

    public int GetPackedState(int towerIndex)
    {
        lock (_lock)
        {
            return _packedState[towerIndex];
        }
    }

    public bool IsValid(int towerIndex)
    {
        lock (_lock)
        {
            return _valid[towerIndex];
        }
    }

    public TowerSiegePhase GetPhase(int towerIndex)
    {
        lock (_lock)
        {
            return PhaseOf(towerIndex);
        }
    }

    public byte? GetControllingTribe(int towerIndex)
    {
        lock (_lock)
        {
            return _controllingTribe[towerIndex];
        }
    }

    /// <summary>
    ///     The full 12-tower ownership/status snapshot (legacy <c>B_BROADCAST_CHUGSOUNG_INFO</c>) as one flat,
    ///     read-only batch -- every caller that needs this exact payload shares this one projection instead of
    ///     re-deriving it: <see cref="World.Zone" />'s post-siege-start rebroadcast
    ///     (<c>ApplyTowerGuardianHitSideEffects</c>) and the unconditional per-zone-entry snapshot the Services
    ///     layer sends alongside the RvR world-info broadcast both read the identical 12-tower state, taken
    ///     under one lock so no in-flight <see cref="BeginUpgrade" />/<see cref="CompleteUpgrade" /> can be
    ///     observed half-applied across the batch.
    /// </summary>
    public TowerStatusResponse BuildStatusSnapshot()
    {
        var state1 = new int[TowerCount];
        var state2 = new int[TowerCount];

        lock (_lock)
        {
            for (var i = 0; i < TowerCount; i++)
            {
                state1[i] = _packedState[i];
                // Legacy mState2Tower is filled -1 on every DB read and never appears in any SQL query of its
                // own (ServerDocs/10_ts25center/02_HeroRank_Guilde_Discord_Votes.md:296-298) -- dead, always -1.
                state2[i] = -1;
            }
        }

        return new TowerStatusResponse { State1Tower = state1, State2Tower = state2 };
    }

    /// <summary>The queued level/type for a tower currently in <see cref="TowerSiegePhase.Building" />; 0 otherwise.</summary>
    public int GetPendingPackedStateForBuilding(int towerIndex)
    {
        lock (_lock)
        {
            return _pendingPackedState[towerIndex];
        }
    }

    /// <summary>
    ///     Legacy <c>MyGame::GetTowerState</c> -- 0 if untouched (packed &lt; 1), else the level digits (packed/100):
    ///     2/4/6/8.
    /// </summary>
    public static int DecodeLevel(int packedState)
    {
        return packedState < 1 ? 0 : packedState / 100;
    }

    /// <summary>Legacy <c>MyGame::GetTowerType</c> -- 0 if untouched, else the type digits (packed%100): 1/2/3.</summary>
    public static int DecodeType(int packedState)
    {
        return packedState < 1 ? 0 : packedState % 100;
    }

    /// <summary>
    ///     Recomputes every tribe's Silver/CP-for-PvM/CP-for-PvP/XP tower bonus from the 12 towers' current
    ///     packed state -- legacy's <c>ResetTowerBonus</c> + <c>UpdateTowerBonus</c>, called once per tick by
    ///     <see cref="TowerRewardBonusSystem" /> (unconditionally, for every zone -- not gated to the 12
    ///     tower-hosting ones, matching legacy's own per-process cadence). See
    ///     <see cref="TowerRewardBonusTable" />'s remarks for the overwrite-not-additive semantics.
    /// </summary>
    public void RecomputeTribeBonuses()
    {
        lock (_lock)
        {
            var recomputed = TowerRewardBonusTable.Recompute(_packedState);
            recomputed.CopyTo(_tribeBonus);
        }
    }

    /// <summary>
    ///     This tribe's tower bonus as of the last <see cref="RecomputeTribeBonuses" /> call --
    ///     <see cref="TowerTribeRewardBonus.None" /> for any tribe outside 0-3 (<c>MAX_TRIBE_NUM</c>) or before
    ///     the first recompute.
    /// </summary>
    public TowerTribeRewardBonus GetTribeBonus(byte tribe)
    {
        lock (_lock)
        {
            return tribe < _tribeBonus.Length ? _tribeBonus[tribe] : TowerTribeRewardBonus.None;
        }
    }

    /// <summary>
    ///     Reserved <c>MonsterEntity.ServerIndex</c> for a tower's guardian: negative, so it can never collide
    ///     with <c>MonsterSpawnScheduler</c>'s own positive, per-zone slot indices (legacy's counterpart is the
    ///     shmMONSTER_OBJECT reserved <c>START_SPECIAL_MONSTER_OBJECT_NUM..END_SPECIAL_MONSTER_OBJECT_NUM</c> range).
    /// </summary>
    public static int GuardianServerIndex(int towerIndex)
    {
        return -(towerIndex + 1);
    }

    /// <summary>True while this tower's "under siege" broadcast flag is set -- see <see cref="_underAttack" />'s remarks.</summary>
    public bool IsUnderAttack(int towerIndex)
    {
        lock (_lock)
        {
            return _underAttack[towerIndex];
        }
    }

    /// <summary>UTC moment of this guardian instance's first landed hit since it was last (re)spawned, if any.</summary>
    public DateTime? GetFirstAttackAtUtc(int towerIndex)
    {
        lock (_lock)
        {
            return _firstAttackAtUtc[towerIndex];
        }
    }

    /// <summary>UTC moment of this guardian instance's most recent landed hit, if any.</summary>
    public DateTime? GetLastAttackAtUtc(int towerIndex)
    {
        lock (_lock)
        {
            return _lastAttackAtUtc[towerIndex];
        }
    }

    /// <summary>
    ///     Legacy <c>SetAttackTower(0)</c> + <c>mTowerPostTick</c> refresh (S07_MyGame02.cpp:2146-2147): call
    ///     once per allowed, landed hit against this tower's guardian (never on a miss, never on a
    ///     gate-rejected attack -- see <see cref="Progression.TowerFriendlyFireGate" />). Unconditionally clears
    ///     the "under siege" broadcast flag and refreshes the last-attack timestamp on every call.
    /// </summary>
    /// <returns>
    ///     True only for the very first landed hit against this particular guardian instance since it was last
    ///     (re)spawned (<see cref="CompleteUpgrade" /> resets the underlying flag) -- callers use that to gate
    ///     the one-time Center notification + full tower-state rebroadcast (S07_MyGame02.cpp:2148-2153).
    /// </returns>
    public bool RecordGuardianHit(int towerIndex, DateTime utcNow)
    {
        lock (_lock)
        {
            _underAttack[towerIndex] = false;
            _lastAttackAtUtc[towerIndex] = utcNow;

            if (_firstAttackRecorded[towerIndex])
                return false;

            _firstAttackRecorded[towerIndex] = true;
            _firstAttackAtUtc[towerIndex] = utcNow;
            return true;
        }
    }

    /// <summary>Raw setter for tests/seeding -- production code drives towers through the methods below instead.</summary>
    public void SetTowerState(int towerIndex, int packedState, bool valid)
    {
        lock (_lock)
        {
            _packedState[towerIndex] = packedState;
            _valid[towerIndex] = valid;
        }
    }

    /// <summary>
    ///     CZ_CHUGSOUNG_WAR_UP_SEND's success path (S04_MyWork02.cpp:14422-14423): the target level/type is
    ///     recorded and <see cref="IsValid" /> clears immediately (blocking a resubmission), but the broadcast
    ///     packed state and the guardian monster itself don't move until <see cref="TowerGuardianSystem" />'s
    ///     next tick actually (re)spawns it -- see <see cref="CompleteUpgrade" />.
    /// </summary>
    public void BeginUpgrade(int towerIndex, int newPackedState, byte controllingTribeId)
    {
        lock (_lock)
        {
            _valid[towerIndex] = false;
            _pendingPackedState[towerIndex] = newPackedState;
            _pendingTribe[towerIndex] = controllingTribeId;
        }
    }

    /// <summary>
    ///     <see cref="Progression.TowerGuardianSystem" /> calls this once the new guardian has actually been
    ///     spawned into the tower's zone -- the packed state (and hence <see cref="GetPackedState" />'s decoded
    ///     level/type) only becomes visible from here on, matching legacy's own <c>UpdateTowerValue</c> timing.
    /// </summary>
    public void CompleteUpgrade(int towerIndex)
    {
        lock (_lock)
        {
            var pending = _pendingPackedState[towerIndex];
            if (pending <= 0)
                return;

            _packedState[towerIndex] = pending;
            _controllingTribe[towerIndex] = _pendingTribe[towerIndex];
            _pendingPackedState[towerIndex] = 0;
            _pendingTribe[towerIndex] = null;
            _valid[towerIndex] = true;
            _siegeStartedAtUtc[towerIndex] = null;
            _dirty[towerIndex] = true;

            // A freshly (re)spawned guardian instance starts its own hit bookkeeping over -- legacy resets the
            // same one-shot flag whenever a new guardian is summoned (S10_MySummon.cpp:2218-2220).
            _underAttack[towerIndex] = false;
            _firstAttackRecorded[towerIndex] = false;
            _firstAttackAtUtc[towerIndex] = null;
            _lastAttackAtUtc[towerIndex] = null;
        }
    }

    /// <summary>
    ///     The guardian monster is gone (killed by another tribe) -- legacy state 9, starting the destroy
    ///     cooldown. Idempotent: a tower already sieged keeps its original <paramref name="utcNow" />.
    /// </summary>
    public void BeginSiege(int towerIndex, DateTime utcNow)
    {
        lock (_lock)
        {
            if (_siegeStartedAtUtc[towerIndex] is not null)
                return;

            _valid[towerIndex] = false;
            _siegeStartedAtUtc[towerIndex] = utcNow;
        }
    }

    /// <summary>True once <see cref="SiegeCollapseCooldown" /> has elapsed since <see cref="BeginSiege" />.</summary>
    public bool IsDueForDestruction(int towerIndex, DateTime utcNow)
    {
        lock (_lock)
        {
            return _siegeStartedAtUtc[towerIndex] is { } startedAt && utcNow - startedAt >= SiegeCollapseCooldown;
        }
    }

    /// <summary>
    ///     Legacy state 90 -&gt; 0 (S07_MyGame01.cpp:13767-13774): the tower fully reverts to uncontrolled/unbuilt.
    ///     Rebuilding it from here needs the item-use "construct tower" flow (case 665), out of scope for this
    ///     cluster -- see <see cref="TowerWarState" />'s own remarks.
    /// </summary>
    public void CompleteDestruction(int towerIndex)
    {
        lock (_lock)
        {
            _packedState[towerIndex] = 0;
            _controllingTribe[towerIndex] = null;
            _siegeStartedAtUtc[towerIndex] = null;
            _valid[towerIndex] = false;
            _pendingPackedState[towerIndex] = 0;
            _pendingTribe[towerIndex] = null;
            _dirty[towerIndex] = true;

            // A11 -- a destroyed slot returns fully to idle: any in-flight construction/cooldown/heal for the
            // now-gone tower is discarded so a subsequent item-665 can start cleanly from state 0.
            _constructKind[towerIndex] = 0;
            _createCooldownStartedAtUtc[towerIndex] = null;
            _guardianHealPending[towerIndex] = false;
        }
    }

    // ---------------------------------------------------------------------------------------------------------
    // A11 -- from-scratch construction (item 665), the tower-attack AI's idle timers, and the item-667 heal
    // handoff. All lifecycle advance runs on the tower's own zone tick via TowerLifecycleSystem; the item-use
    // handler thread only ever arms these here. Construction deliberately does NOT set _pendingPackedState/_valid
    // while "creating", so a tower under construction reads as TowerSiegePhase.Dormant to the existing
    // TowerGuardianSystem (whose Dormant branch is a no-op) -- the two systems drive disjoint states with no edit
    // to TowerGuardianSystem needed. Contract: S04_MyWork03.cpp:7283-7565, S07_MyGame01.cpp:13659-13780,
    // S07_MyGame02.cpp:2107-2157, S07_MyGame05.cpp:838-863.
    // ---------------------------------------------------------------------------------------------------------

    /// <summary>
    ///     Arms an item-665 from-scratch construction: records the requested kind and controlling tribe for a
    ///     tower that is currently fully idle (legacy state 0, no live guardian, no pending upgrade, not already
    ///     constructing). Atomic idle-claim -- returns false, changing nothing, if the slot is not idle (including
    ///     losing a race to a second concurrent item-665 on the same tower), so the caller can retain the item.
    ///     The level-1 guardian is summoned by <see cref="TowerLifecycleSystem" /> on the tower's next tick, not
    ///     here (contract side effect A.2-A.3).
    /// </summary>
    public bool BeginConstruction(int towerIndex, int constructKind, byte controllingTribeId)
    {
        lock (_lock)
        {
            var idle = _packedState[towerIndex] == 0 && !_valid[towerIndex]
                                                     && _siegeStartedAtUtc[towerIndex] is null
                                                     && _pendingPackedState[towerIndex] == 0
                                                     && _constructKind[towerIndex] == 0;
            if (!idle)
                return false;

            _constructKind[towerIndex] = constructKind;
            _pendingTribe[towerIndex] = controllingTribeId;
            return true;
        }
    }

    /// <summary>
    ///     Rolls back a <see cref="BeginConstruction" /> claim (used when the item-consumption DB write that was
    ///     meant to follow it fails) -- returns the slot to idle so the retained item can be used again.
    /// </summary>
    public void CancelConstruction(int towerIndex)
    {
        lock (_lock)
        {
            if (_createCooldownStartedAtUtc[towerIndex] is not null)
                return; // already spawned/cooling -- past the point a plain cancel is safe

            _constructKind[towerIndex] = 0;
            _pendingTribe[towerIndex] = null;
        }
    }

    /// <summary>The queued construct kind (1/2/3) for a tower still "creating" its level-1 guardian; 0 otherwise.</summary>
    public int GetPendingConstructKind(int towerIndex)
    {
        lock (_lock)
        {
            return _createCooldownStartedAtUtc[towerIndex] is null ? _constructKind[towerIndex] : 0;
        }
    }

    /// <summary>
    ///     Legacy creating-state completion (S07_MyGame01.cpp:13465-13489): once the level-1 guardian has actually
    ///     been summoned, the slot value becomes level-1 of the constructed kind (packed = 200 + kind, i.e.
    ///     DecodeLevel 2), ownership is recorded, the attack-state resets to "ready", the tower is marked invalid
    ///     pending the create-cooldown, and the post-create cooldown clock starts. The tower stays NOT
    ///     <see cref="IsValid" /> (hence not attackable) until <see cref="CompleteConstructionCooldown" />.
    /// </summary>
    public void CompleteConstructionSpawn(int towerIndex, DateTime utcNow)
    {
        lock (_lock)
        {
            var kind = _constructKind[towerIndex];
            if (kind == 0)
                return;

            // Level-1 created tower of this kind: DecodeLevel(200 + kind) == 2, DecodeType == kind.
            _packedState[towerIndex] = 200 + kind;
            _controllingTribe[towerIndex] = _pendingTribe[towerIndex];
            _pendingTribe[towerIndex] = null;
            _constructKind[towerIndex] = 0;
            _createCooldownStartedAtUtc[towerIndex] = utcNow;
            _valid[towerIndex] = false;
            _dirty[towerIndex] = true;

            // Fresh guardian instance: attack-state "ready" (-1) and hit bookkeeping reset, same as CompleteUpgrade.
            _underAttack[towerIndex] = false;
            _firstAttackRecorded[towerIndex] = false;
            _firstAttackAtUtc[towerIndex] = null;
            _lastAttackAtUtc[towerIndex] = null;
        }
    }

    /// <summary>True once <see cref="CreateCooldown" /> has elapsed since <see cref="CompleteConstructionSpawn" />.</summary>
    public bool IsCreateCooldownElapsed(int towerIndex, DateTime utcNow)
    {
        lock (_lock)
        {
            return _createCooldownStartedAtUtc[towerIndex] is { } startedAt && utcNow - startedAt >= CreateCooldown;
        }
    }

    /// <summary>
    ///     Legacy state 1 -&gt; 2 (S07_MyGame01.cpp:13714-13722): the post-create cooldown has elapsed, so the
    ///     level-1 tower becomes live and attackable (<see cref="IsValid" /> true, <see cref="GetPhase" /> Active).
    /// </summary>
    public void CompleteConstructionCooldown(int towerIndex)
    {
        lock (_lock)
        {
            if (_createCooldownStartedAtUtc[towerIndex] is null)
                return;

            _createCooldownStartedAtUtc[towerIndex] = null;
            _valid[towerIndex] = true;
        }
    }

    /// <summary>
    ///     Count of towers of <paramref name="kind" /> that currently exist cluster-wide -- both live/cooling
    ///     built towers (packed state present with this DecodeType) and towers still "creating" this kind --
    ///     for the item-665 max-of-a-kind gate (contract edge case, S04_MyWork03.cpp:7340-7355).
    /// </summary>
    public int CountTowersOfKind(int kind)
    {
        lock (_lock)
        {
            var count = 0;
            for (var i = 0; i < TowerCount; i++)
                if ((_packedState[i] > 0 && DecodeType(_packedState[i]) == kind) || _constructKind[i] == kind)
                    count++;

            return count;
        }
    }

    /// <summary>
    ///     True if another slot in <paramref name="towerIndex" />'s own 3-slot tribe group already holds (or is
    ///     constructing) <paramref name="kind" /> -- the intent of the un-opened <c>CHUGSOUNG_WAR_UI_CheckTowerType</c>
    ///     group-adjacency reject (contract edge case, S04_MyWork03.cpp:7357-7494). Only the "rejects a duplicate
    ///     kind within the tribe's 3-slot group" intent is relied on here; flagged for re-check.
    /// </summary>
    public bool IsKindPresentInTribeGroup(int towerIndex, int kind)
    {
        lock (_lock)
        {
            var groupStart = towerIndex / 3 * 3;
            for (var i = groupStart; i < groupStart + 3 && i < TowerCount; i++)
            {
                if (i == towerIndex)
                    continue;

                if ((_packedState[i] > 0 && DecodeType(_packedState[i]) == kind) || _constructKind[i] == kind)
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    ///     Tower-attack AI ~30s idle reset (S07_MyGame05.cpp:844-863, contract side effect D): once
    ///     <see cref="AttackStateIdleReset" /> has elapsed with no further landed hit, the guardian's attack-state
    ///     returns to "ready". Modeled against <see cref="_underAttack" />'s existing polarity -- a landed hit
    ///     clears the flag (<see cref="RecordGuardianHit" />), this idle timer sets it back, exactly as that
    ///     field's own remarks describe the (previously out-of-scope) timer doing. No-op until at least one hit has
    ///     landed. Returns true only on the tick that actually performed the reset.
    /// </summary>
    public bool TryResetIdleAttackState(int towerIndex, DateTime utcNow)
    {
        lock (_lock)
        {
            if (_underAttack[towerIndex] || _lastAttackAtUtc[towerIndex] is not { } last)
                return false;

            if (utcNow - last < AttackStateIdleReset)
                return false;

            _underAttack[towerIndex] = true;
            return true;
        }
    }

    /// <summary>
    ///     Tower-attack AI 10-minute engagement auto-clear (S07_MyGame05.cpp:857, contract side effect D): once an
    ///     engagement's first landed hit is <see cref="EngagementAutoClear" /> old, the one-shot first-attack
    ///     tracking is reset so a subsequent hit is again treated as a fresh "first hit" (re-arming the one-time
    ///     Center-754 notification via <see cref="RecordGuardianHit" />). Returns true only on the clearing tick.
    /// </summary>
    public bool TryClearStaleEngagement(int towerIndex, DateTime utcNow)
    {
        lock (_lock)
        {
            if (!_firstAttackRecorded[towerIndex] || _firstAttackAtUtc[towerIndex] is not { } first)
                return false;

            if (utcNow - first < EngagementAutoClear)
                return false;

            _firstAttackRecorded[towerIndex] = false;
            _firstAttackAtUtc[towerIndex] = null;
            return true;
        }
    }

    /// <summary>
    ///     Arms an item-667 guardian heal for tick-side application (guardian +10% max life, clamped, then a
    ///     repair-info broadcast -- the actual life mutation is tick-owned and handed off, see this file's remarks
    ///     and the wiring manifest). Idempotent: a heal already pending stays pending.
    /// </summary>
    public void RequestGuardianHeal(int towerIndex)
    {
        lock (_lock)
        {
            _guardianHealPending[towerIndex] = true;
        }
    }

    /// <summary>True while an item-667 heal is queued for this tower's guardian.</summary>
    public bool IsGuardianHealPending(int towerIndex)
    {
        lock (_lock)
        {
            return _guardianHealPending[towerIndex];
        }
    }

    /// <summary>
    ///     Drains a queued item-667 heal for tick-side application -- returns true (and clears the flag) exactly
    ///     once per queued heal. Called only from the tower's own zone tick (see the wiring manifest's heal-drain
    ///     handoff).
    /// </summary>
    public bool TryConsumeGuardianHeal(int towerIndex)
    {
        lock (_lock)
        {
            if (!_guardianHealPending[towerIndex])
                return false;

            _guardianHealPending[towerIndex] = false;
            return true;
        }
    }

    /// <summary>
    ///     Idempotent-bootstrap-then-load: seeds game.TowerState's 12 rows if this is a first boot, then hydrates
    ///     this cache from them. A tower already built (Level &gt; 0) resumes into <see cref="TowerSiegePhase.Building" />
    ///     rather than straight to <see cref="TowerSiegePhase.Active" />, since the guardian <c>MonsterEntity</c>
    ///     itself is pure in-memory and never survives the restart -- <see cref="Progression.TowerGuardianSystem" />
    ///     (re)spawns it on that tower's zone's very next tick, same as a fresh upgrade. Must complete before any
    ///     zone accepts a connection.
    /// </summary>
    public async Task InitializeAsync(ITowerRepository towers, CancellationToken ct)
    {
        await towers.EnsureInitializedAsync(ct).ConfigureAwait(false);
        var rows = await towers.GetAllAsync(ct).ConfigureAwait(false);

        lock (_lock)
        {
            foreach (var row in rows)
            {
                if (row.TowerIndex >= TowerCount)
                    continue; // defensive only -- game.TowerState's own CHECK constraint already enforces 0-11

                _controllingTribe[row.TowerIndex] = row.ControllingTribeId;

                if (row.Level > 0)
                {
                    _pendingPackedState[row.TowerIndex] = row.Level * 100 + row.TowerType;
                    _pendingTribe[row.TowerIndex] = row.ControllingTribeId;
                }
            }
        }
    }

    /// <summary>
    ///     Persists every tower touched by <see cref="CompleteUpgrade" />/<see cref="CompleteDestruction" /> since
    ///     the last flush; no-ops otherwise. Never throws -- a failed write is logged and left dirty for the next
    ///     interval to retry, matching <see cref="World.WorldState.WorldStateService.FlushIfDirtyAsync" />'s own contract.
    /// </summary>
    public async Task FlushDirtyAsync(ITowerRepository towers, CancellationToken ct)
    {
        for (var i = 0; i < TowerCount; i++)
        {
            int packed;
            byte? tribe;

            lock (_lock)
            {
                if (!_dirty[i])
                    continue;

                packed = _packedState[i];
                tribe = _controllingTribe[i];
                _dirty[i] = false;
            }

            try
            {
                await towers.SetProgressAsync((byte)i, (byte)DecodeLevel(packed), (byte)DecodeType(packed), tribe, ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    _dirty[i] = true;
                }

                logger?.LogError(ex, "TowerState flush failed for tower {TowerIndex} -- will retry next interval", i);
            }
        }
    }

    private TowerSiegePhase PhaseOf(int towerIndex)
    {
        if (_siegeStartedAtUtc[towerIndex] is not null)
            return TowerSiegePhase.Sieged;
        if (_pendingPackedState[towerIndex] > 0)
            return TowerSiegePhase.Building;
        return _valid[towerIndex] ? TowerSiegePhase.Active : TowerSiegePhase.Dormant;
    }
}
