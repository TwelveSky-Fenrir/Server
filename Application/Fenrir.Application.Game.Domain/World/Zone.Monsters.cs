using System.Buffers;
using System.Collections.Concurrent;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    /// <summary>
    ///     A009 flinch damage threshold -- <c>(float)shmMONSTER_INFO-&gt;mLife * 0.10f</c>
    ///     (<c>Server/ts25zone/S07_MyGame02.cpp:2475</c>), see <see cref="TryApplyPvmFlinch" />.
    /// </summary>
    private const float FlinchDamageThresholdRatio = 0.10f;

    /// <summary>
    ///     Dedicated <see cref="MonsterEntity.ServerIndex" /> pool for the Elevated-tier "moncall" GM command
    ///     (tSort 506) -- same "each ad-hoc/reserved spawn family gets its own non-overlapping base" convention
    ///     already established by <c>ZoneWar.TribeGuardSpawner.OrdinaryPoolServerIndexBase</c>/
    ///     <c>Zone038WinnerPoolServerIndexBase</c> (1_000_000/1_001_000) and
    ///     <c>ZoneWar.TribeSymbolSpawner.SymbolPoolServerIndexBase</c> (1_002_000, size 100) -- well clear of
    ///     <see cref="Monsters.MonsterSpawnScheduler" />'s own ordinary per-zone slot numbering (1..
    ///     <c>RegularMonsterTableCapacity</c> = 3400) and of <see cref="SummonPersonalBoss" />'s own
    ///     character-id-keyed slot reuse. A purely internal Fenrir bookkeeping choice -- legacy's own
    ///     <c>SummonMonsterForSpecial</c> has no equivalent slot-numbering concept for this GM command to port.
    /// </summary>
    private const int GmSummonPoolServerIndexBase = 1_003_000;

    /// <summary>Size of <see cref="GmSummonPoolServerIndexBase" />'s reserved range.</summary>
    private const int GmSummonPoolSize = 1_000;

    /// <summary>
    ///     Free-roam leash for a "moncall"-summoned monster -- same value as <see cref="PersonalDungeonBossLeashRadius" />
    ///     (Zone.DungeonInstance.cs), reused here as a reasonable boss-scale default since "moncall" is most
    ///     often used to test/battle a boss-tier monster; the source behavior contract for tSort 506 does not
    ///     itself specify a leash radius (the underlying <c>SummonMonsterForSpecial</c> primitive is not
    ///     independently modeled in Fenrir with its own leash parameter for this call).
    /// </summary>
    private const float GmSummonLeashRadius = 200f;

    /// <summary>
    ///     Enqueued by <see cref="TryDamageMonster" /> (any thread) on a killing blow, drained by
    ///     <see cref="Monsters.MonsterSpawnScheduler" /> on this zone's own next tick (single-writer preserved).
    /// </summary>
    private readonly ConcurrentQueue<DeadMonsterEvent> _deadMonsters = new();

    /// <summary>
    ///     Released once per queued grant so <c>MonsterLootFlushHost</c> can flush as soon as a grant arrives
    ///     instead of waiting up to a full flush interval, shrinking the in-memory-only loss window to roughly
    ///     one SQL round trip.
    /// </summary>
    private readonly SemaphoreSlim _moneyGrantSignal = new(0, int.MaxValue);

    /// <summary>
    ///     Reusable scratch buffer for <see cref="BroadcastMonsterAction" />'s recipient list -- same
    ///     non-allocating shape and reuse justification as <see cref="Zone.PlayerLifecycle" />'s
    ///     <c>_rebroadcastNeighborScratch</c>: single tick thread, cleared immediately before each call, never
    ///     read after the immediately-following send loop returns. Replaces a per-call
    ///     <c>AoiGrid.Neighbors(cell).ToArray()</c> (iterator + LINQ buffer) that used to run once per due
    ///     monster -- during a keep-alive burst, once per due monster in that SAME tick, not once per zone.
    /// </summary>
    private readonly List<int> _monsterBroadcastNeighborScratch = [];

    /// <summary>
    ///     Monster-side counterpart to <see cref="_grid" />, keyed by <see cref="MonsterEntity.ServerIndex" />
    ///     instead of character id -- lets <see cref="SendExistingMonstersTo" /> query nearby monsters directly
    ///     instead of scanning every monster this zone holds. Tick-owned only, same posture as <see cref="_grid" />
    ///     itself: every mutation (<see cref="SpawnMonster" />, <see cref="SyncMonsterCell" />,
    ///     <see cref="RemoveMonsterFromGrid" />) runs only from this zone's own tick thread, including the
    ///     death path -- <see cref="TryDamageMonster" /> removes the dying monster from <see cref="_monsters" />
    ///     (safe from any thread) but deliberately leaves THIS grid alone, deferring the matching removal to
    ///     <see cref="Monsters.MonsterSpawnScheduler.DrainDeaths" /> on this zone's own next tick.
    /// </summary>
    private readonly AoiGrid _monsterGrid = new(options.AoiCellSize);

    // Same ConcurrentDictionary posture as _players -- the tick is the sole writer for spawn/AI mutation, but
    // TryDamageMonster is a deliberate exception letting a combat packet handler thread apply damage directly
    // via an atomic Interlocked path on MonsterEntity itself.
    private readonly ConcurrentDictionary<int, MonsterEntity> _monsters = new();

    /// <summary>
    ///     Reusable scratch buffer for <see cref="ResolveMonsterAttack" />'s raw AOI-neighbor scan, drained into
    ///     <see cref="_mvpAttackRecipientScratch" /> immediately after -- same non-allocating shape and reuse
    ///     justification as <see cref="_monsterBroadcastNeighborScratch" />: single tick thread, cleared before
    ///     use, never read after the immediately-following broadcast returns.
    /// </summary>
    private readonly List<int> _mvpAttackNeighborScratch = [];

    /// <summary>
    ///     Reusable scratch buffer for <see cref="ResolveMonsterAttack" />'s target+neighbors AOI recipient set
    ///     -- replaces a per-attack <c>new HashSet&lt;int&gt;()</c>. Same single-tick-thread reuse posture as
    ///     every other scratch buffer in this file family.
    /// </summary>
    private readonly HashSet<int> _mvpAttackRecipientScratch = [];

    /// <summary>
    ///     Server-initiated monster-kill money grants, queued rather than awaited inline because
    ///     <see cref="Tick" /> is fully synchronous and must never block on SQL I/O; drained by
    ///     <see cref="MonsterLootFlushHost" /> from any thread.
    /// </summary>
    private readonly ConcurrentQueue<(int CharacterId, long Amount)> _pendingMoneyGrants = new();

    /// <summary>
    ///     Reusable scratch buffer for <see cref="SendExistingMonstersTo" />'s <see cref="_monsterGrid" />
    ///     neighbor scan -- replaces the enumerable-returning, iterator-allocating
    ///     <see cref="AoiGrid.Neighbors(ValueTuple{int,int},float,float,float,int)" /> overload with the
    ///     non-allocating buffer overload. Single tick thread, cleared before use, consumed entirely by the
    ///     immediately-following per-monster send loop before <see cref="SendExistingMonstersTo" /> returns.
    /// </summary>
    private readonly List<int> _sendExistingMonstersScratch = [];

    private int _monsterUniqueNumberSeed;

    public int MonsterCount => _monsters.Count;

    public IEnumerable<MonsterEntity> MonstersSnapshot => _monsters.Values;

    public bool TryGetMonster(int serverIndex, out MonsterEntity? monster)
    {
        return _monsters.TryGetValue(serverIndex, out monster);
    }

    public uint NextMonsterUniqueNumber()
    {
        return unchecked((uint)Interlocked.Increment(ref _monsterUniqueNumberSeed));
    }

    /// <summary>
    ///     Tick-owned caller only (<see cref="Monsters.MonsterSpawnScheduler" />). Registers <paramref name="monster" />
    ///     in both <see cref="_monsters" /> and <see cref="_monsterGrid" /> (at its already-resolved spawn
    ///     position) before announcing it.
    /// </summary>
    public void SpawnMonster(MonsterEntity monster)
    {
        // Staggered, not a plain "= _clock": MonsterSpawnScheduler.Simulate's InitialPopDone branch spawns
        // EVERY configured spawn-region slot unconditionally on a zone's first Simulate call, all in the same
        // Zone.Tick, all reading the exact same _clock value -- without this offset every one of those monsters
        // would become due for RebroadcastMonsters' 5 s keep-alive on the exact same later tick, re-synchronize
        // (that tick re-stamps all of them to that same new _clock value), and repeat forever: a thundering herd
        // of individual MonsterReplicationResponse sends recurring every 5 s for the zone's whole lifetime. See
        // SimulationClock.RebroadcastStaggerOffset's own remarks.
        monster.LastRebroadcastAt = _clock - SimulationClock.RebroadcastStaggerOffset(monster.ServerIndex,
            SimulationClock.MonsterRebroadcastInterval);
        _monsters[monster.ServerIndex] = monster;

        var cell = _grid.CellOf(monster.PosX, monster.PosZ);
        monster.CurrentCell = cell;
        _monsterGrid.Add(monster.ServerIndex, cell, monster.PosX, monster.PosY, monster.PosZ);

        BroadcastMonsterAction(monster, 1); // action=1 on B_MONSTER_ACTION_RECV at creation
    }

    /// <summary>
    ///     Tick-owned caller only (<see cref="Progression.TowerGuardianSystem" />). Removes a monster outright --
    ///     no loot, no <see cref="DeadMonsterEvent" />, no death broadcast -- mirroring legacy <c>FreeTower</c>
    ///     (S07_MyGame01.cpp:13642-13657), which just invalidates the old guardian's shared-memory slot before a
    ///     stronger one replaces it on upgrade.
    /// </summary>
    public void DespawnMonsterSilently(int serverIndex)
    {
        if (_monsters.TryRemove(serverIndex, out var monster))
            RemoveMonsterFromGrid(monster);
    }

    /// <summary>
    ///     Tick-owned caller only -- unregisters <paramref name="monster" /> from <see cref="_monsterGrid" /> at
    ///     its own last-synced <see cref="MonsterEntity.CurrentCell" />. Every direct <see cref="_monsters" />
    ///     removal elsewhere in this partial class family (<see cref="DespawnMonsterSilently" />,
    ///     <see cref="Monsters.MonsterSpawnScheduler.DrainDeaths" />, <see cref="SummonPersonalBoss" />'s slot
    ///     reuse, <see cref="ClearZone241PersonalDungeonInstance" />'s instance sweep) must pair with a call
    ///     here so <see cref="_monsterGrid" /> never accumulates a stale entry for a monster that no longer
    ///     exists in <see cref="_monsters" />.
    /// </summary>
    public void RemoveMonsterFromGrid(MonsterEntity monster)
    {
        _monsterGrid.Remove(monster.ServerIndex, monster.CurrentCell);
    }

    /// <summary>
    ///     Tick-owned caller only (<see cref="Monsters.MonsterAiSystem" />, once per monster per AI pass, after
    ///     whatever position mutation that pass applied) -- the monster-side counterpart to
    ///     <see cref="HandleMove" />'s own <c>_grid.Move</c> call for players. Keeps <see cref="_monsterGrid" />
    ///     in sync with <paramref name="monster" />'s live position -- cell MEMBERSHIP churn (the
    ///     dictionary/hash-set mutation
    ///     <see cref="AoiGrid.Move(int,ValueTuple{int,int},ValueTuple{int,int},float,float,float)" />
    ///     itself skips when <c>from == to</c>) still only happens on the minority of passes that actually
    ///     crossed a cell boundary, same as before. Always still refreshes the monster's own tracked exact
    ///     position, though, even within the same cell -- <see cref="AoiGrid" />'s exact-distance pass
    ///     (<see cref="SendExistingMonstersTo" />'s own query) would otherwise compare against a stale position
    ///     for a monster that keeps moving inside one cell without ever crossing into a new one (windup/chase
    ///     micro-movement is exactly that case).
    /// </summary>
    public void SyncMonsterCell(MonsterEntity monster)
    {
        var newCell = _grid.CellOf(monster.PosX, monster.PosZ);
        _monsterGrid.Move(monster.ServerIndex, monster.CurrentCell, newCell, monster.PosX, monster.PosY,
            monster.PosZ);
        monster.CurrentCell = newCell;
    }

    /// <summary>
    ///     Legacy <c>mDATA.mIndex</c> values that unconditionally override <see cref="SelectMonsterKillCredit" />
    ///     to the killing blow's own attacker regardless of what the ordinary damage-based path would have
    ///     picked (<c>Server/ts25zone/S07_MyGame02.cpp:2802-2828</c>). Monster 561 in the same source block is
    ///     a confirmed-unrelated branch (<c>BossWarDrop</c>) and does not belong here.
    /// </summary>
    private static bool IsKillingBlowOverrideMonster(int monsterId)
    {
        return monsterId is 746 or 777 or 1407 or 1408 or 1404;
    }

    /// <summary>
    ///     Safe from any thread (see <see cref="MonsterEntity.TakeDamage" />'s remarks). On the killing blow,
    ///     atomically removes the monster from the live pool and queues a <see cref="DeadMonsterEvent" /> for
    ///     this zone's own next tick to process -- never processed inline here.
    /// </summary>
    /// <remarks>
    ///     Every hit -- not just the killing one -- accrues onto <paramref name="attackerCharacterId" />'s own
    ///     tracked damage-history entry on <paramref name="serverIndex" />'s <see cref="MonsterEntity" />
    ///     (legacy <c>SetAttackInfoWithAvatar</c>, <c>S07_MyGame05.cpp:1675-1720</c>) before the HP mutation
    ///     below, so a killing hit's own damage is already counted by the time
    ///     <see cref="SelectMonsterKillCredit" /> runs. Only resolvable-in-<see cref="_players" /> attackers are
    ///     tracked at all -- an unresolvable <paramref name="attackerCharacterId" /> (environment damage, a
    ///     stale/disconnected id) contributes no entry, matching legacy's own "no tracked attacker, no credit"
    ///     outcome.
    /// </remarks>
    public bool TryDamageMonster(int serverIndex, int amount, int? attackerCharacterId, out bool died,
        out int remainingLife)
    {
        if (!_monsters.TryGetValue(serverIndex, out var monster))
        {
            died = false;
            remainingLife = 0;
            return false;
        }

        if (attackerCharacterId is { } attackerId && _players.TryGetValue(attackerId, out var attackerState))
            monster.RegisterAttackDamage(attackerId, attackerState, amount);

        died = monster.TakeDamage(amount, out remainingLife);
        if (died)
        {
            _monsters.TryRemove(serverIndex, out _);
            var creditedCharacterId = SelectMonsterKillCredit(monster, attackerCharacterId);
            _deadMonsters.Enqueue(new DeadMonsterEvent(monster, creditedCharacterId));
        }

        return true;
    }

    /// <summary>
    ///     <c>ProcessAttack03</c>'s kill-credit dispatch on <c>mSpecialSortNumber</c>
    ///     (<c>S07_MyGame02.cpp:2794-2830</c>): the five hardcoded boss ids
    ///     (<see cref="IsKillingBlowOverrideMonster" />) always credit the killing blow
    ///     (<paramref name="killingBlowAttackerId" />) regardless of <see cref="MonsterEntity.SpecialSort" />;
    ///     otherwise only a <see cref="Monsters.MonsterSpecialSort.Standard" /> (class 1, "standard monster")
    ///     kill credits anyone at all -- the single highest cumulative tracked damage, via
    ///     <see cref="SelectDamageBasedKillCredit" />. A null result (class isn't Standard and no override
    ///     applies, OR the class is Standard but no eligible damage-history entry exists) leaves the kill fully
    ///     unattributed -- <see cref="Monsters.MonsterSpawnScheduler.ProcessDeath" /> already gates both the
    ///     loot-drop and experience-grant calls on this being non-null, matching legacy's own
    ///     <c>tSelectAvatarIndex == -1</c> gate (<c>S07_MyGame02.cpp:2830</c>, reused at <c>:3173-3176</c> for
    ///     experience).
    /// </summary>
    /// <remarks>
    ///     Behavior contract <c>A3-kill-credit-class</c>: legacy's own death-time recipient-decision switch
    ///     (<c>S07_MyGame02.cpp:2794-2799</c>) has exactly one matching case -- class 1 -- and no default; every
    ///     other reachable class (2, 3, 4, 5, 6, 10) falls through with no recipient computed here, independent
    ///     of whether that class ever wrote entries into the shared 50-slot attacker table in the first place.
    ///     <para>
    ///         Class 6 ("car-thrower") is the one genuinely surprising case: legacy DOES register class-6
    ///         attackers into the same shared table classes 1 does (<c>S07_MyGame02.cpp:2163-2169,2459-2468</c>),
    ///         but the recipient-decision switch still has no case for it -- so a class-6 kill tracks damage
    ///         data that is never read back for reward purposes. Fenrir does not attempt to replicate that
    ///         write-side asymmetry (<see cref="TryDamageMonster" /> registers every class unconditionally,
    ///         a harmless superset since this method already filters non-Standard classes out on the read
    ///         side) -- only the class filter here, which is the one place the asymmetry is actually
    ///         observable, is modeled.
    ///     </para>
    ///     <para>
    ///         Classes 2 ("tribe/holy stone") and 10 (tower) are already special-cased through entirely
    ///         separate mechanisms that never consult this table -- the tribe-symbol "Holy Stone" per-faction
    ///         accumulator (<see cref="Monsters.MonsterSpawnScheduler.ProcessDeath" />'s own tribe-symbol
    ///         branch) and tower guardians (identified by their own reserved negative
    ///         <see cref="MonsterEntity.ServerIndex" /> range, see <see cref="ApplyPvmAttack" />'s remarks) --
    ///         this class filter is a no-op harness for both, not their actual gate.
    ///     </para>
    ///     <para>
    ///         The one extra tribe-state side effect the source contract notes as unique to override id 1407 is
    ///         deliberately not modeled: it needs a piece of round-scoped state no Fenrir equivalent has been
    ///         identified for yet.
    ///     </para>
    ///     <para>
    ///         Open question (source contract, unrecoverable from <c>Server/</c>): whether the five override
    ///         catalog ids actually carry class 1 in their template data (making the override redundant with
    ///         the Standard-only rule below) or a different class (making it a genuine carve-out) cannot be
    ///         determined -- the override is applied unconditionally either way, exactly matching legacy, so
    ///         this ambiguity has no effect on the code here.
    ///     </para>
    /// </remarks>
    private int? SelectMonsterKillCredit(MonsterEntity monster, int? killingBlowAttackerId)
    {
        if (killingBlowAttackerId is { } blowAttacker && IsKillingBlowOverrideMonster(monster.Template.MonsterId))
            return blowAttacker;

        // Behavior contract A3-kill-credit-class, side effect 1: only class 1 ("standard monster") ever
        // resolves a recipient through the generic max-damage path. Every other reachable class (2, 3, 4, 5,
        // 6, 10) has no matching case in legacy's own recipient-decision switch, so the kill-credit
        // restriction is enforced right here, before ever touching the damage-history table.
        if (monster.SpecialSort != MonsterSpecialSort.Standard)
            return null;

        return SelectDamageBasedKillCredit(monster);
    }

    /// <summary>
    ///     <c>SelectAvatarIndexForMaxAttackDamage</c> (<c>S07_MyGame05.cpp:1723-1780</c>): the single highest
    ///     cumulative-damage entry among every still-eligible tracked attacker, or null if none qualify. The
    ///     first eligible entry is accepted unconditionally as the initial candidate regardless of its own
    ///     recorded damage (<c>:1763-1766</c>) -- including an exact zero if that slot originated from
    ///     <see cref="MonsterEntity.RegisterAcquisition" />'s write-through rather than ever being hit; every
    ///     subsequent eligible entry only replaces the current candidate on a strictly-greater damage value, so
    ///     an exact tie is won by whichever entry was tracked first -- <see cref="MonsterEntity.SnapshotAttackDamage" />
    ///     preserves oldest-to-newest order, and only a strict improvement ever replaces the current leader.
    /// </summary>
    private int? SelectDamageBasedKillCredit(MonsterEntity monster)
    {
        int? bestCharacterId = null;
        long? bestDamage = null;

        foreach (var entry in monster.SnapshotAttackDamage())
        {
            // Not resolvable in _players == not session-ready, or gone (logged out / mid zone-transfer):
            // Fenrir's _players only ever holds a fully-entered, non-transferring player (see HandleEnter), so
            // a missing lookup here already collapses legacy's separate isReady/isTransferringZone checks into
            // a single absence check.
            if (!_players.TryGetValue(entry.CharacterId, out var candidate))
                continue;

            // Stale slot: a different login has since re-entered under this same character id (HandleEnter
            // always builds a fresh PlayerRuntimeState), so this entry no longer belongs to a live session.
            if (!ReferenceEquals(candidate, entry.SessionToken))
                continue;

            if (candidate.IsDead)
                continue;

            // Hidden/stealthed re-check is NOT modeled: PlayerRuntimeState has no stealth/hide state yet, so
            // this fifth legacy eligibility check cannot be applied today -- a documented gap, not a bug.

            if (bestDamage is null || entry.CumulativeDamage > bestDamage.Value)
            {
                bestDamage = entry.CumulativeDamage;
                bestCharacterId = entry.CharacterId;
            }
        }

        return bestCharacterId;
    }

    public bool TryDequeueDeadMonster(out DeadMonsterEvent? deadMonster)
    {
        return _deadMonsters.TryDequeue(out deadMonster);
    }

    /// <summary>
    ///     Tick-owned caller only -- sets the transient <see cref="MonsterAiState.Dead" /> value and broadcasts the final
    ///     (LifeValue == 0) frame.
    /// </summary>
    public void BroadcastMonsterDeath(MonsterEntity monster)
    {
        monster.AiState = MonsterAiState.Dead;
        BroadcastMonsterAction(monster, 0);
    }

    /// <summary>
    ///     A009 hit-stagger ("vacillement") trigger -- the piece <see cref="MonsterAiState.Flinch" />'s own
    ///     remarks flagged as living in <see cref="ApplyPvmAttack" /> but "not wired yet." The state's own
    ///     tick-countdown-then-return-to-<see cref="MonsterAiState.Decision" /> behavior
    ///     (<see cref="Monsters.MonsterAiSystem" />) was already fully implemented and only needed this caller.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : <c>Server/ts25zone/S07_MyGame02.cpp:2471-2487</c> (<c>ProcessAttack03</c>, immediately
    ///     after the per-attacker damage-history accumulation). Gated, in this exact short-circuit order --
    ///     preserved so a PRNG draw only ever happens on the same calls legacy itself draws on --
    ///     on: <paramref name="monster" />'s <c>shmMONSTER_INFO-&gt;mDamageType != 1</c> (stationary/structure
    ///     monsters, e.g. the tribe-symbol stones, never flinch, and this check is cheap/first so those
    ///     monsters never consume a roll here), a 50% <c>rand_mir()</c> roll, this single hit's own
    ///     <paramref name="damageDealt" /> exceeding 10% of <see cref="MonsterEntity.MaxLife" />, and the
    ///     monster not already mid-flinch (<c>aSort != 8</c>). Only reachable for a hit the monster survived --
    ///     see the caller in <see cref="ApplyPvmAttack" />, matching legacy's own <c>mDATA.mLifeValue &gt; 0</c>
    ///     outer gate at the same source lines. On success, <c>aSort</c> is set to 8 (A009) and
    ///     <c>mTRANSFER.B_MONSTER_ACTION_RECV(..., 1)</c> is sent -- <c>checkChangeActionState=1</c>, the same
    ///     "this is a new action the client must render" convention <see cref="SpawnMonster" /> uses at
    ///     creation, not the keep-alive/no-change 0 <see cref="RebroadcastMonsters" /> uses.
    ///     <para>
    ///         NOT reproduced: the facing-angle update (<c>aFront = GetYAngle(...)</c>, turning the monster to
    ///         face its attacker) and the RvR-only <c>SendSpecialNumber()</c> call in the same legacy block --
    ///         both are outside this fix's own source contract (attack-target-resolution); flag separately if
    ///         byte-exact facing/heading parity on a flinch is ever needed.
    ///     </para>
    /// </remarks>
    private void TryApplyPvmFlinch(MonsterEntity monster, int damageDealt)
    {
        if (monster.Template.DamageType == 1)
            return;

        if (_random.NextInt32(2) != 0)
            return;

        if (damageDealt <= (int)(monster.MaxLife * FlinchDamageThresholdRatio))
            return;

        if (monster.AiState == MonsterAiState.Flinch)
            return;

        monster.AiState = MonsterAiState.Flinch;
        monster.StateTicks = 0;
        BroadcastMonsterAction(monster, 1);
    }

    /// <summary>
    ///     Identifier-1407 "Elite Boss" kill announcement (<c>World.Loot.BossEventDropResolver</c>) --
    ///     <c>U_ZONE_BROADCAST_FOR_CENTER_SEND(2003, ...)</c> has no receiving Center process in Fenrir's
    ///     two-executable topology, the same collapse <see cref="ApplyTowerGuardianHitSideEffects" />'s own Center
    ///     hop (broadcast code 754) already uses -- logged rather than sent; no further modeled client-facing
    ///     consequence exists today.
    /// </summary>
    /// <remarks>
    ///     The source behavior contract this ports flags an open question: this exact broadcast may fire TWICE
    ///     per kill in the legacy source (once unconditionally near the top of <c>ProcessForDropItem</c>,
    ///     <c>Server/ts25zone/S07_MyGame05.cpp:2090-2099</c>, and again inside the guaranteed-drop block,
    ///     <c>:2509-2529</c>) -- only the second, drop-tier-scoped firing is reproduced here; the first was
    ///     outside this method's own source contract and needs <c>cpp-zone-gameplay-analyst</c> re-verification
    ///     before a second call site is added.
    /// </remarks>
    public void AnnounceEliteBossDefeated(byte killerTribe, string killerName)
    {
        logger.LogInformation(
            "Elite Boss defeated (Center broadcast 2003): killerTribe={KillerTribe} killerName={KillerName} zone={MapId}",
            killerTribe, killerName, MapId);
    }

    /// <summary>
    ///     AI-initiated MvP attack -- the monster's own AI calls this directly, never via a client packet
    ///     (<c>S07_MyGame05.cpp:3961</c>). Runs on this zone's own tick thread.
    /// </summary>
    public void ResolveMonsterAttack(MonsterEntity monster, int targetCharacterId)
    {
        if (!_players.TryGetValue(targetCharacterId, out var target) || target is null)
            return;

        var defenderSnapshot = ToCombatantSnapshot(target);
        var outcome = MonsterCombatResolver.ResolveMvpAttack(monster, defenderSnapshot, _clock, _random);
        if (outcome.Rejected)
            return;

        var response = new AttackResponse
        {
            AttackInfo = new AttackForProtocol
            {
                Case = 4,
                ServerIndex1 = monster.ServerIndex,
                UniqueNumber1 = monster.UniqueNumber,
                ServerIndex2 = target.CharacterId,
                UniqueNumber2 = target.UniqueNumber,
                SenderLocation = [monster.PosX, monster.PosY, monster.PosZ],
                AttackActionValue1 = 1,
                AttackActionValue2 = 0,
                AttackActionValue3 = 0,
                AttackActionValue4 = 0,
                AttackResultValue = outcome.Hit ? 1 : 0,
                AttackCriticalExist = outcome.Critical ? 1 : 0,
                AttackElementDamage = outcome.ElementDamage,
                // View = full (pre-life-cap) hit size the client displays; Real = the life-capped amount
                // actually applied -- MvP shares the same split (S07_MyGame02.cpp:3428-3433).
                AttackViewDamageValue = outcome.ViewDamage,
                AttackRealDamageValue = outcome.DamageApplied
            }
        };

        _mvpAttackRecipientScratch.Clear();
        _mvpAttackRecipientScratch.Add(target.CharacterId);

        _mvpAttackNeighborScratch.Clear();
        _grid.Neighbors(_mvpAttackNeighborScratch, target.CurrentCell, target.PosX, target.PosY, target.PosZ);
        foreach (var id in _mvpAttackNeighborScratch)
            _mvpAttackRecipientScratch.Add(id);

        BroadcastAttackResult(_mvpAttackRecipientScratch, response);

        if (!outcome.Hit)
            return;

        target.Life -= outcome.DamageApplied;
        target.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);

        if (target.Life <= 0)
            ApplyDeath(target.CharacterId, DeathCause.MonsterKill);
    }

    public void QueueMoneyGrant(int characterId, long amount)
    {
        _pendingMoneyGrants.Enqueue((characterId, amount));
        _moneyGrantSignal.Release();
    }

    /// <summary>
    ///     Resolves as soon as a grant is queued (or immediately, if one is already pending un-awaited) -- lets
    ///     <c>MonsterLootFlushHost</c> race this against its own periodic timer via <c>Task.WhenAny</c> rather
    ///     than only ever waking up on the timer's fixed cadence.
    /// </summary>
    public Task WaitForMoneyGrantAsync(CancellationToken ct)
    {
        return _moneyGrantSignal.WaitAsync(ct);
    }

    /// <summary>Callable from any thread; the only intended caller is the background flush host.</summary>
    public IReadOnlyList<(int CharacterId, long Amount)> DrainPendingMoneyGrants()
    {
        if (_pendingMoneyGrants.IsEmpty)
            return [];

        List<(int CharacterId, long Amount)>? grants = null;
        while (_pendingMoneyGrants.TryDequeue(out var grant))
            (grants ??= []).Add(grant);

        return (IReadOnlyList<(int CharacterId, long Amount)>?)grants ?? [];
    }

    /// <summary>
    ///     Immediate monster-visibility exchange on zone entry -- closes the gap where a monster already alive
    ///     in this zone was otherwise invisible to a new arrival until that monster's own next independent 5 s
    ///     keep-alive fired (<see cref="RebroadcastMonsters" />). Sends a direct, one-shot, keep-alive-shaped
    ///     (<c>checkChangeActionState = 2</c>, "re-sync to this state") replication frame to
    ///     <paramref name="state" />'s own session for every monster within <paramref name="state" />'s
    ///     immediate AOI neighborhood (the same 3x3-cell scoping <see cref="BroadcastMonsterAction" /> already
    ///     uses from the monster's own side, applied here by symmetry since the neighbor relation is reciprocal)
    ///     and dungeon-instance visible (<see cref="IsVisibleAcrossDungeonInstance" />) to it -- mirroring the
    ///     mutual player-to-player visibility exchange <see cref="HandleEnter" /> already performs for avatars.
    /// </summary>
    /// <remarks>
    ///     Re-verified 2026-07: ts25zone has no dedicated on-enter monster burst -- neither the periodic tick
    ///     (<c>Server/ts25zone/S07_MyGame01.cpp:2518-2567</c>) nor <c>ZoneWorker::UpdateMonster</c>
    ///     (<c>Server/ts25zone/ZoneWorker.cpp:112-155</c>) sends anything targeted at a single entering user, and
    ///     no per-enter monster loop exists in the enter path; the legacy client already knows monster spawns
    ///     from its own map data and is driven purely by the action broadcasts (state-change + special-monster
    ///     keep-alive). This method therefore remains a deliberate Fenrir-only latency improvement -- a
    ///     well-formed <c>checkChangeActionState = 2</c> re-sync of the server's authoritative state, the exact
    ///     shape the keep-alive sends, just delivered eagerly on entry instead of up to 5 s later. It is a pure
    ///     one-way, one-shot send: no other session's own view of any monster changes because of this arrival,
    ///     and no timer/state is touched -- <see cref="MonsterEntity.LastRebroadcastAt" /> keeps running on its
    ///     own independent cadence regardless of whether this method ever runs.
    ///     <para>
    ///         Queries <see cref="_monsterGrid" /> instead of scanning every <see cref="_monsters" /> entry --
    ///         this runs once per player <see cref="HandleEnter" />, so on a busy zone the old brute-force scan
    ///         cost every monster in the zone once per arriving player, not once per tick. A grid entry can
    ///         very rarely point at a <see cref="MonsterEntity.ServerIndex" /> that <see cref="_monsters" /> no
    ///         longer holds (this same tick already despawned/killed it before this call ran) -- the lookup
    ///         below simply skips it, exactly as harmless as the old scan not finding it would have been.
    ///     </para>
    ///     <para>
    ///         Uses the legacy-parity exact-distance overload at the base (scale-1) radius, not each candidate
    ///         monster's own <see cref="Monsters.MonsterBroadcastScale" />-derived scale -- unlike
    ///         <see cref="BroadcastMonsterAction" />, this whole mechanism has no legacy citation to begin with
    ///         (see the caveat above), so there is no cited per-candidate scale to widen by here either; this is
    ///         a deliberate, uniform simplification rather than a re-guess.
    ///     </para>
    /// </remarks>
    private void SendExistingMonstersTo(PlayerRuntimeState state)
    {
        var cell = state.CurrentCell;
        if (!_monsterGrid.HasAnyNeighbor(cell))
            return;

        _sendExistingMonstersScratch.Clear();
        _monsterGrid.Neighbors(_sendExistingMonstersScratch, cell, state.PosX, state.PosY, state.PosZ);
        foreach (var serverIndex in _sendExistingMonstersScratch)
        {
            if (!_monsters.TryGetValue(serverIndex, out var monster))
                continue; // stale grid entry (despawned/killed earlier this same tick) -- harmless, skip

            if (!IsVisibleAcrossDungeonInstance(monster.InstanceId, state.DungeonInstanceId))
                continue;

            // checkChangeActionState = 2 (re-sync), the same value the 5 s keep-alive uses -- this is a
            // catch-up of pre-existing state, not a fresh action. Never 0 (never a value legacy sends for a
            // monster action frame); see RebroadcastMonsters' remarks.
            state.Session.Send(BuildMonsterActionRecv(monster, 2));
        }
    }

    /// <summary>
    ///     Immediate monster action/state-change broadcast (legacy <c>checkChangeActionState = 1</c>) -- the
    ///     tick-owned caller (<see cref="Monsters.MonsterAiSystem" />) fires this at every FSM transition that
    ///     changes the monster's visible action or target, matching legacy's unconditional
    ///     <c>B_MONSTER_ACTION_RECV(..., 1)</c> + <c>Send1</c>/<c>Send2</c>/<c>Send3</c> at each such transition
    ///     (
    ///     <c>
    ///         Server/ts25zone/S07_MyGame05.cpp:1027-1028,1063-1064,1172-1173,1354-1356,1362-1364,1380-1381,
    ///         1671-1672
    ///     </c>
    ///     ). This is the piece that was missing: previously monsters only ever reached clients via
    ///     the 5 s keep-alive below, so a real client learned about an aggro/attack/return only up to 5 s late
    ///     (and, before the descriptor fix, malformed) -- which desynced and reset the client's monster
    ///     simulation. <c>1</c> tells the client "this is a new action, render it," distinct from the keep-alive's
    ///     <c>2</c> ("re-sync to this state").
    /// </summary>
    public void BroadcastMonsterActionChange(MonsterEntity monster)
    {
        BroadcastMonsterAction(monster, 1);
    }

    /// <summary>
    ///     Keep-alive rebroadcast for monsters, 5 s cadence. <c>checkChangeActionState = 2</c>: legacy's own
    ///     periodic monster catch-up uses <c>2</c> ("re-sync to this state"), never <c>0</c>
    ///     (<c>Server/ts25zone/S07_MyGame01.cpp:2548</c>, <c>Server/ts25zone/ZoneWorker.cpp:132</c> --
    ///     <c>B_MONSTER_ACTION_RECV(..., 2)</c>; the <c>0</c> literal in <c>Send1</c>/<c>Send2</c>/<c>Send3</c> is
    ///     the <c>Broadcast11</c> <c>type</c> argument selecting the send buffer, NOT this field, which the
    ///     earlier revision conflated). <c>0</c> is never a value legacy sends for a monster action frame.
    /// </summary>
    private void RebroadcastMonsters()
    {
        foreach (var monster in _monsters.Values)
        {
            if (_clock - monster.LastRebroadcastAt < SimulationClock.MonsterRebroadcastInterval)
                continue;

            monster.LastRebroadcastAt = _clock;
            BroadcastMonsterAction(monster, 2);
        }
    }

    /// <summary>
    ///     Serialize-once broadcast for monster replication -- same pattern as <see cref="BroadcastAvatarAction" />,
    ///     including the same <see cref="IsReviveHackBroadcastSuppressed" /> per-recipient gate (see that method's
    ///     own remarks for the legacy citations establishing both broadcast families route through the same
    ///     MyUtil::Broadcast11 primitive).
    ///     <see cref="AoiGrid.HasAnyNeighbor" /> pre-checks emptiness before paying for
    ///     <see cref="_monsterBroadcastNeighborScratch" />'s scan; see that field's own remarks for why this is a
    ///     reused non-allocating buffer rather than the enumerable-returning, iterator-plus-LINQ-<c>ToArray()</c>
    ///     overload of <c>AoiGrid.Neighbors</c>. Scale resolved per-monster via
    ///     <see cref="Monsters.MonsterBroadcastScale" /> -- see that class's own remarks for the full legacy
    ///     citation chain (periodic catch-up always dispatches through <c>SendSpecialNumber</c>, so this is not
    ///     a guess).
    /// </summary>
    private void BroadcastMonsterAction(MonsterEntity monster, int checkChangeActionState)
    {
        var scale = MonsterBroadcastScale.ForMonster(monster.Template.Type, monster.Template.SpecialType);
        var cell = _grid.CellOf(monster.PosX, monster.PosZ);
        if (!_grid.HasAnyNeighbor(cell, scale))
            return;

        _monsterBroadcastNeighborScratch.Clear();
        _grid.Neighbors(_monsterBroadcastNeighborScratch, cell, monster.PosX, monster.PosY, monster.PosZ, scale);
        var packet = BuildMonsterActionRecv(monster, checkChangeActionState);
        var total = FrameWriter.FrameSizeOf<MonsterReplicationResponse>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in packet, span);

            foreach (var id in _monsterBroadcastNeighborScratch)
                try
                {
                    if (_players.TryGetValue(id, out var recipient) &&
                        recipient.Session is ClientSession clientSession &&
                        IsVisibleAcrossDungeonInstance(monster.InstanceId, recipient.DungeonInstanceId) &&
                        !IsReviveHackBroadcastSuppressed(recipient))
                        clientSession.SendRaw(span);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Zone {MapId} monster broadcast to character {RecipientId} failed", MapId,
                        id);
                }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>
    ///     Elevated-tier "moncall" GM command (tSort 506) -- see
    ///     <see cref="Tribes.TribeProgressZoneCommand.GmSummonMonsterTemplateId" />'s own remarks for the
    ///     posting side. Silently does nothing if <paramref name="monsterId" /> does not resolve in the world
    ///     catalog (legacy performs no validation of its own either -- see the source behavior contract's own
    ///     Edge cases) or if <see cref="GmSummonPoolServerIndexBase" />'s reserved range is exhausted (a
    ///     Fenrir-side safeguard with no legacy equivalent, since legacy's own bounded world-object-pool
    ///     exhaustion failure mode is already masked from the wire by this command's own unconditional success
    ///     result -- same posture <see cref="Fenrir.Application.Game.Services.Gm.GmCreateItemService" />'s own
    ///     remarks document for its sibling spawn-item command). No duplicate-of-same-template check: legacy's
    ///     own <c>SummonMonsterForSpecial</c> call passes <c>tCheckExistMonster=FALSE</c> for this command, and
    ///     Fenrir's own <see cref="_monsters" /> is keyed by slot, not by template id, so there was never a
    ///     duplicate check here to turn off in the first place. No owner binding: <see cref="MonsterEntity.Create" />'s
    ///     optional <c>instanceId</c> parameter is left at its "no owner" default, matching legacy's own omitted
    ///     <c>tUserIndex</c> argument.
    /// </summary>
    private void SpawnGmSummonedMonster(int monsterId, PlayerRuntimeState state)
    {
        if (!worldData.MonstersById.TryGetValue(monsterId, out var definition))
            return;

        if (!TryFindFreeGmSummonSlot(out var serverIndex))
            return;

        var monster = MonsterEntity.Create(serverIndex, NextMonsterUniqueNumber(), definition.Monster, serverIndex,
            state.PosX, state.PosY, state.PosZ, GmSummonLeashRadius);

        SpawnMonster(monster);
    }

    private bool TryFindFreeGmSummonSlot(out int serverIndex)
    {
        for (var i = 0; i < GmSummonPoolSize; i++)
        {
            var candidate = GmSummonPoolServerIndexBase + i;
            if (!_monsters.ContainsKey(candidate))
            {
                serverIndex = candidate;
                return true;
            }
        }

        serverIndex = 0;
        return false;
    }

    /// <summary>
    ///     Builds the op18 monster-action replication frame from the monster's cached action descriptor.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : the target descriptor mirrors <c>mDATA.mAction</c> as legacy fills it at each state
    ///     transition. When a target is locked, <c>aTargetObjectIndex</c> and <c>aTargetObjectUniqueNumber</c>
    ///     are the pursued avatar's own index+unique number (never one without the other --
    ///     <c>Server/ts25zone/S07_MyGame05.cpp:945-946,1104-1105,1373-1374</c>), and <c>aTargetLocation</c> is
    ///     the movement/attack destination toward that avatar. When idle, legacy's <c>aTargetObjectIndex</c> is
    ///     <c>-1</c> (NOT 0 -- 0 is a valid avatar slot the client would resolve as a real player), its unique
    ///     number 0, and <c>aTargetObjectSort</c> 0 (the monster-spawn init at
    ///     <c>Server/ts25zone/S10_MySummon.cpp:784-786</c>; <c>aTargetObjectSort</c> is never reassigned for a
    ///     monster anywhere in the AI update, so it stays 0). The previous implementation hardcoded index
    ///     <c>?? 0</c>, unique number 0, and <c>aTargetLocation</c> to the monster's own position -- a malformed
    ///     descriptor a wire-bot stores harmlessly but a real client's renderer chokes on.
    /// </remarks>
    private static MonsterReplicationResponse BuildMonsterActionRecv(MonsterEntity monster,
        int checkChangeActionState)
    {
        // Idle: index -1 (legacy sentinel, S10_MySummon.cpp:785 -- NOT 0, which is a valid avatar slot), unique
        // number 0. Locked: the pursued avatar's own index + unique number, always paired.
        var targetIndex = monster.TargetCharacterId ?? -1;
        var targetUniqueNumber = monster.TargetCharacterId is null ? 0 : unchecked((int)monster.TargetUniqueNumber);
        return new MonsterReplicationResponse
        {
            ServerIndex = monster.ServerIndex,
            UniqueNumber = monster.UniqueNumber,
            Data = new ObjectForMonster
            {
                Index = monster.Template.MonsterId,
                Action = new ActionInfo
                {
                    Type = 0,
                    Sort = (int)monster.AiState,
                    Frame = 0,
                    Location = [monster.PosX, monster.PosY, monster.PosZ],
                    TargetLocation = [monster.TargetLocationX, monster.TargetLocationY, monster.TargetLocationZ],
                    Front = monster.Heading,
                    TargetFront = monster.Heading,
                    PetLocation = new float[3],
                    PetTargetLocation = new float[3],
                    PetFront = 0,
                    PetSort = 0,
                    TargetObjectSort = 0,
                    TargetObjectIndex = targetIndex,
                    TargetObjectUniqueNumber = targetUniqueNumber,
                    SkillNumber = 0,
                    SkillGradeNum1 = 0,
                    SkillGradeNum2 = 0,
                    SkillValue = 0
                },
                LifeValue = monster.Life
            },
            CheckChangeActionState = checkChangeActionState
        };
    }
}
