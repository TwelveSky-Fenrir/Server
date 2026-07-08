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
    ///     Legacy <c>destroyCoolDown</c> = 5.0f minutes (<c>GetGameTickMinute(5f)</c> = 600 legacy ticks @ 500 ms
    ///     = 300 s), the wait in legacy state 90 between the guardian dying and the tower fully reverting to 0
    ///     (S07_MyGame01.cpp:13662,13768).
    /// </summary>
    public static readonly TimeSpan SiegeCollapseCooldown = TimeSpan.FromMinutes(5);

    private readonly byte?[] _controllingTribe = new byte?[TowerCount];
    private readonly bool[] _dirty = new bool[TowerCount];

    private readonly DateTime?[] _firstAttackAtUtc = new DateTime?[TowerCount];

    /// <summary>One-shot per guardian lifetime, reset alongside everything else in <see cref="CompleteUpgrade" />.</summary>
    private readonly bool[] _firstAttackRecorded = new bool[TowerCount];

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
