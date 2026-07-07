using System.Buffers;
using System.Collections.Concurrent;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Data.WriteBehind;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
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

    // Same ConcurrentDictionary posture as _players -- the tick is the sole writer for spawn/AI mutation, but
    // TryDamageMonster is a deliberate exception letting a combat packet handler thread apply damage directly
    // via an atomic Interlocked path on MonsterEntity itself.
    private readonly ConcurrentDictionary<int, MonsterEntity> _monsters = new();

    /// <summary>
    ///     Server-initiated monster-kill money grants, queued rather than awaited inline because
    ///     <see cref="Tick" /> is fully synchronous and must never block on SQL I/O; drained by
    ///     <see cref="MonsterLootFlushHost" /> from any thread.
    /// </summary>
    private readonly ConcurrentQueue<(int CharacterId, long Amount)> _pendingMoneyGrants = new();

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

    /// <summary>Tick-owned caller only (<see cref="Monsters.MonsterSpawnScheduler" />).</summary>
    public void SpawnMonster(MonsterEntity monster)
    {
        monster.LastRebroadcastAt = _clock;
        _monsters[monster.ServerIndex] = monster;
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
        _monsters.TryRemove(serverIndex, out _);
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
    ///     (<paramref name="killingBlowAttackerId" />); every other monster -- the "ordinary" category, the
    ///     vast majority, the default/fallthrough result of legacy's own <c>ReturnSpecialSortNumber</c>
    ///     (<c>Server/ts25zone/S10_MySummon.cpp:612-647</c>) -- instead credits whichever tracked attacker dealt
    ///     the single highest cumulative damage, via <see cref="SelectDamageBasedKillCredit" />. A null result
    ///     (no eligible damage-history entry, or none tracked at all) leaves the kill fully unattributed --
    ///     <see cref="Monsters.MonsterSpawnScheduler.ProcessDeath" /> already gates both the loot-drop and
    ///     experience-grant calls on this being non-null, matching legacy's own <c>tSelectAvatarIndex == -1</c>
    ///     gate (<c>S07_MyGame02.cpp:2830</c>, reused at <c>:3173-3176</c> for experience).
    /// </summary>
    /// <remarks>
    ///     Categories other than "ordinary" that legacy also accumulates this same damage table for (e.g.
    ///     category 6, <c>S07_MyGame02.cpp:2459-2468</c>) have no matching case in legacy's own death-time
    ///     dispatch either (<c>S07_MyGame02.cpp:2794-2799</c>) -- Fenrir does not model
    ///     <c>mSpecialSortNumber</c> as its own field today, so every monster other than the five override ids
    ///     is treated as "ordinary" here. This intentionally does not chase down every legacy category (an open
    ///     question the source contract itself flags as unresolved, not something to guess at); it is exactly
    ///     the fix this method exists for, and it does not disturb the two categories Fenrir already
    ///     special-cases through entirely separate mechanisms that never consult this table -- the tribe-symbol
    ///     "Holy Stone" per-faction accumulator (<see cref="Monsters.MonsterSpawnScheduler.ProcessDeath" />'s own
    ///     tribe-symbol branch) and tower guardians (identified by their own reserved negative
    ///     <see cref="MonsterEntity.ServerIndex" /> range, see <see cref="ApplyPvmAttack" />'s remarks).
    ///     <para>
    ///         The one extra tribe-state side effect the source contract notes as unique to override id 1407 is
    ///         deliberately not modeled: it needs a piece of round-scoped state no Fenrir equivalent has been
    ///         identified for yet.
    ///     </para>
    /// </remarks>
    private int? SelectMonsterKillCredit(MonsterEntity monster, int? killingBlowAttackerId)
    {
        if (killingBlowAttackerId is { } blowAttacker && IsKillingBlowOverrideMonster(monster.Template.MonsterId))
            return blowAttacker;

        return SelectDamageBasedKillCredit(monster);
    }

    /// <summary>
    ///     <c>SelectAvatarIndexForMaxAttackDamage</c> (<c>S07_MyGame05.cpp:1723-1780</c>): the single highest
    ///     cumulative-damage entry among every still-eligible tracked attacker, or null if none qualify. The
    ///     strictly-greater-than comparison means an exact tie is won by whichever entry was tracked first --
    ///     <see cref="MonsterEntity.SnapshotAttackDamage" /> preserves oldest-to-newest order, and only a strict
    ///     improvement ever replaces the current leader.
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
                AttackViewDamageValue = outcome.DamageApplied,
                AttackRealDamageValue = outcome.DamageApplied
            }
        };

        var recipients = new HashSet<int> { target.CharacterId };
        foreach (var id in _grid.Neighbors(target.CurrentCell)) recipients.Add(id);
        BroadcastAttackResult(recipients, response);

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
    ///     (<c>checkChangeActionState = 0</c>) replication frame to <paramref name="state" />'s own session for
    ///     every monster within <paramref name="state" />'s immediate AOI neighborhood (the same 3x3-cell
    ///     scoping <see cref="BroadcastMonsterAction" /> already uses from the monster's own side, applied here
    ///     by symmetry since the neighbor relation is reciprocal) and dungeon-instance visible
    ///     (<see cref="IsVisibleAcrossDungeonInstance" />) to it -- mirroring the mutual player-to-player
    ///     visibility exchange <see cref="HandleEnter" /> already performs for avatars.
    /// </summary>
    /// <remarks>
    ///     No legacy citation confirms this restores ts25zone parity rather than diverging from it -- the
    ///     behavior contract this was implemented from flags the underlying legacy behavior as unconfirmed (no
    ///     <c>Server/</c> source located for what, if anything, ts25zone itself sends about pre-existing
    ///     monsters on a player's zone entry). This is a pure one-way, one-shot send: no other session's own
    ///     view of any monster changes because of this arrival, and no timer/state is touched --
    ///     <see cref="MonsterEntity.LastRebroadcastAt" /> keeps running on its own independent cadence
    ///     regardless of whether this method ever runs.
    /// </remarks>
    private void SendExistingMonstersTo(PlayerRuntimeState state)
    {
        if (_monsters.IsEmpty)
            return;

        foreach (var monster in _monsters.Values)
        {
            var monsterCell = _grid.CellOf(monster.PosX, monster.PosZ);
            if (Math.Abs(monsterCell.X - state.CurrentCell.X) > 1 ||
                Math.Abs(monsterCell.Z - state.CurrentCell.Z) > 1)
                continue;

            if (!IsVisibleAcrossDungeonInstance(monster.InstanceId, state.DungeonInstanceId))
                continue;

            state.Session.Send(BuildMonsterActionRecv(monster, 0));
        }
    }

    /// <summary>Keep-alive rebroadcast for monsters, 5 s cadence.</summary>
    private void RebroadcastMonsters()
    {
        foreach (var monster in _monsters.Values)
        {
            if (_clock - monster.LastRebroadcastAt < SimulationClock.MonsterRebroadcastInterval)
                continue;

            monster.LastRebroadcastAt = _clock;
            BroadcastMonsterAction(monster, 0);
        }
    }

    /// <summary>Serialize-once broadcast for monster replication -- same pattern as <see cref="BroadcastAvatarAction" />.</summary>
    private void BroadcastMonsterAction(MonsterEntity monster, int checkChangeActionState)
    {
        var recipients = NeighborsOfPosition(monster.PosX, monster.PosZ).ToArray();
        if (recipients.Length == 0)
            return;

        var packet = BuildMonsterActionRecv(monster, checkChangeActionState);
        var total = FrameWriter.FrameSizeOf<MonsterReplicationResponse>();
        var rented = ArrayPool<byte>.Shared.Rent(total);

        try
        {
            var span = rented.AsSpan(0, total);
            FrameWriter.WriteFrame(in packet, span);

            foreach (var id in recipients)
                try
                {
                    if (_players.TryGetValue(id, out var recipient) &&
                        recipient.Session is ClientSession clientSession &&
                        IsVisibleAcrossDungeonInstance(monster.InstanceId, recipient.DungeonInstanceId))
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

    private static MonsterReplicationResponse BuildMonsterActionRecv(MonsterEntity monster,
        int checkChangeActionState)
    {
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
                    TargetLocation = [monster.PosX, monster.PosY, monster.PosZ],
                    Front = monster.Heading,
                    TargetFront = monster.Heading,
                    PetLocation = new float[3],
                    PetTargetLocation = new float[3],
                    PetFront = 0,
                    PetSort = 0,
                    TargetObjectSort = 0,
                    TargetObjectIndex = monster.TargetCharacterId ?? 0,
                    TargetObjectUniqueNumber = 0,
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
