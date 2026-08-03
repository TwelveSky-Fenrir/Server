using System.Buffers;
using System.Collections.Concurrent;
using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Core.Packets.Shared;
using Fenrir.Core.Wire;
using Fenrir.Data.WriteBehind;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private const float FlinchDamageThresholdRatio = 0.10f;

    private const int GmSummonPoolServerIndexBase = 1_003_000;

    private const int GmSummonPoolSize = 1_000;

    private readonly ConcurrentQueue<DeadMonsterEvent> _deadMonsters = new();

    private readonly ConcurrentQueue<MonsterEntity> _invalidatedMonsters = new();

    private readonly SemaphoreSlim _moneyGrantSignal = new(0, int.MaxValue);

    private readonly List<int> _monsterBroadcastNeighborScratch = [];

    private readonly AoiGrid _monsterGrid = new(options.AoiCellSize);

    private readonly ConcurrentDictionary<int, MonsterEntity> _monsters = new();

    private readonly List<int> _mvpAttackNeighborScratch = [];

    private readonly HashSet<int> _mvpAttackRecipientScratch = [];

    private readonly ConcurrentQueue<(int CharacterId, long Amount)> _pendingMoneyGrants = new();

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

    public void SpawnMonster(MonsterEntity monster)
    {
        _monsters[monster.ServerIndex] = monster;

        var cell = _grid.CellOf(monster.PosX, monster.PosZ);
        monster.CurrentCell = cell;
        _monsterGrid.Add(monster.ServerIndex, cell, monster.PosX, monster.PosY, monster.PosZ);

        BroadcastMonsterAction(monster, 1);

        monster.LastRebroadcastAt = _clock - SimulationClock.RebroadcastStaggerOffset(monster.ServerIndex,
            SimulationClock.MonsterRebroadcastInterval);

        monster.DetectionThrottleTicks = -SimulationClock.DetectionThrottleStaggerOffsetTicks(monster.ServerIndex);
    }

    public void DespawnMonsterSilently(int serverIndex)
    {
        if (_monsters.TryRemove(serverIndex, out var monster))
            RemoveMonsterFromGrid(monster);
    }

    public void RemoveMonsterFromGrid(MonsterEntity monster)
    {
        _monsterGrid.Remove(monster.ServerIndex, monster.CurrentCell);
    }

    public void SyncMonsterCell(MonsterEntity monster)
    {
        var newCell = _grid.CellOf(monster.PosX, monster.PosZ);
        _monsterGrid.Move(monster.ServerIndex, monster.CurrentCell, newCell, monster.PosX, monster.PosY,
            monster.PosZ);
        monster.CurrentCell = newCell;
    }

    private static bool IsKillingBlowOverrideMonster(int monsterId)
    {
        return monsterId is 746 or 777 or 1407 or 1408 or 1404;
    }

    public bool TryDamageMonster(int serverIndex, int amount, int? attackerCharacterId, out bool died,
        out int remainingLife, bool isCriticalHit = false)
    {
        if (!_monsters.TryGetValue(serverIndex, out var monster))
        {
            died = false;
            remainingLife = 0;
            return false;
        }

        PlayerRuntimeState? attackerState = null;
        if (attackerCharacterId is { } attackerId && _players.TryGetValue(attackerId, out attackerState))
            monster.RegisterAttackDamage(attackerId, attackerState, amount);

        died = monster.TakeDamage(amount, out remainingLife);
        if (died)
        {
            var creditedCharacterId = SelectMonsterKillCredit(monster, attackerCharacterId);

            var killerX = attackerState?.PosX ?? monster.PosX;
            var killerZ = attackerState?.PosZ ?? monster.PosZ;
            MonsterDeathSequence.BeginCorpseCountdown(monster, killerX, killerZ, isCriticalHit, _random);

            _deadMonsters.Enqueue(new DeadMonsterEvent(monster, attackerCharacterId, creditedCharacterId));
        }

        return true;
    }

    public void InvalidateDeadMonster(MonsterEntity monster)
    {
        if (!_monsters.TryRemove(monster.ServerIndex, out _))
            return;

        RemoveMonsterFromGrid(monster);
        _invalidatedMonsters.Enqueue(monster);
    }

    public bool TryDequeueInvalidatedMonster(out MonsterEntity? monster)
    {
        return _invalidatedMonsters.TryDequeue(out monster);
    }

    private int? SelectMonsterKillCredit(MonsterEntity monster, int? killingBlowAttackerId)
    {
        if (killingBlowAttackerId is { } blowAttacker && IsKillingBlowOverrideMonster(monster.Template.MonsterId))
            return blowAttacker;

        if (monster.SpecialSort != MonsterSpecialSort.Standard)
            return null;

        return SelectDamageBasedKillCredit(monster);
    }

    private int? SelectDamageBasedKillCredit(MonsterEntity monster)
    {
        int? bestCharacterId = null;
        long? bestDamage = null;

        foreach (var entry in monster.SnapshotAttackDamage())
        {
            if (!_players.TryGetValue(entry.CharacterId, out var candidate))
                continue;

            if (!ReferenceEquals(candidate, entry.SessionToken))
                continue;

            if (candidate.IsDead)
                continue;

            if (candidate.IsMovingZone)
                continue;

            if (candidate.VisibleState == 0)
                continue;

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

    public void BroadcastMonsterDeath(MonsterEntity monster)
    {
        monster.AiState = MonsterAiState.Dead;
        BroadcastMonsterAction(monster, 0);
    }

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

    public void AnnounceEliteBossDefeated(byte killerTribe, string killerName)
    {
        logger.LogInformation(
            "Elite Boss defeated (Center broadcast 2003): killerTribe={KillerTribe} killerName={KillerName} zone={MapId}",
            killerTribe, killerName, MapId);
    }

    public void ResolveMonsterAttack(MonsterEntity monster, int targetCharacterId, int attackSubMode = 0)
    {
        if (!_players.TryGetValue(targetCharacterId, out var target) || target is null)
            return;

        var defenderSnapshot = ToCombatantSnapshot(target);
        var outcome = MonsterCombatResolver.ResolveMvpAttack(monster, defenderSnapshot, _clock, _random,
            target.VisibleState == 0, target.PshopOpen);
        if (outcome.Rejected)
            return;

        var viewDamage = outcome.ViewDamage;
        var realDamage = outcome.DamageApplied;
        if (outcome.Hit)
        {
            if (MonsterCombatResolver.RollHolyShieldRemoval(monster.Template.SpecialType, attackSubMode, _random))
                RemoveDefenderHolyShields(target);

            (viewDamage, realDamage) =
                ApplyHolyShieldAbsorption(target, outcome, HolyShieldHitByMonsterAvatarChangeInfoSort);
        }

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
                AttackViewDamageValue = viewDamage,
                AttackRealDamageValue = realDamage
            }
        };

        if (!outcome.Hit)
        {
            target.Session.Send(response);
            return;
        }

        target.Life -= realDamage;
        target.MarkProgressDirty(dirtyTracker, DirtyFlags.Vitals);

        _mvpAttackRecipientScratch.Clear();
        _mvpAttackRecipientScratch.Add(target.CharacterId);

        _mvpAttackNeighborScratch.Clear();
        _grid.Neighbors(_mvpAttackNeighborScratch, target.CurrentCell, target.PosX, target.PosY, target.PosZ);
        foreach (var id in _mvpAttackNeighborScratch)
            _mvpAttackRecipientScratch.Add(id);

        BroadcastAttackResult(_mvpAttackRecipientScratch, response, target.DungeonInstanceId);

        if (target.Life <= 0)
            ApplyDeath(target.CharacterId, DeathCause.MonsterKill,
                suppressExperienceLoss: monster.SpecialSort != MonsterSpecialSort.Standard);
    }

    public void QueueMoneyGrant(int characterId, long amount)
    {
        _pendingMoneyGrants.Enqueue((characterId, amount));
        _moneyGrantSignal.Release();
    }

    public Task WaitForMoneyGrantAsync(CancellationToken ct)
    {
        return _moneyGrantSignal.WaitAsync(ct);
    }

    public IReadOnlyList<(int CharacterId, long Amount)> DrainPendingMoneyGrants()
    {
        if (_pendingMoneyGrants.IsEmpty)
            return [];

        List<(int CharacterId, long Amount)>? grants = null;
        while (_pendingMoneyGrants.TryDequeue(out var grant))
            (grants ??= []).Add(grant);

        return (IReadOnlyList<(int CharacterId, long Amount)>?)grants ?? [];
    }

    private void SendExistingMonstersTo(PlayerRuntimeState state)
    {
        var cell = state.CurrentCell;
        if (!_monsterGrid.HasAnyNeighbor(cell, MonsterBroadcastScale.MaxScale))
            return;

        _sendExistingMonstersScratch.Clear();
        _monsterGrid.Neighbors(_sendExistingMonstersScratch, cell, state.PosX, state.PosY, state.PosZ,
            MonsterBroadcastScale.MaxScale);
        foreach (var serverIndex in _sendExistingMonstersScratch)
        {
            if (!_monsters.TryGetValue(serverIndex, out var monster))
                continue;

            if (!IsVisibleAcrossDungeonInstance(monster.InstanceId, state.DungeonInstanceId))
                continue;

            var scale = MonsterBroadcastScale.ForMonster(monster.Template.Type, monster.Template.SpecialType);
            if (!_monsterGrid.IsWithinRadius(serverIndex, state.PosX, state.PosY, state.PosZ, scale))
                continue;

            state.Session.Send(BuildMonsterActionRecv(monster, 2));
        }
    }

    public void BroadcastMonsterActionChange(MonsterEntity monster)
    {
        BroadcastMonsterAction(monster, 1);
    }

    public void BroadcastMonsterPathBlocked(MonsterEntity monster)
    {
        BroadcastMonsterAction(monster, 1);
    }

    private void RebroadcastMonsters()
    {
        foreach (var monster in _monsters.Values)
        {
            if (_clock - monster.LastRebroadcastAt < SimulationClock.MonsterRebroadcastInterval)
                continue;

            BroadcastMonsterAction(monster, 2);
        }
    }

    private void BroadcastMonsterAction(MonsterEntity monster, int checkChangeActionState)
    {
        monster.LastRebroadcastAt = _clock;

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
                    if (TryGetBroadcastRecipient(id, out var recipient, out var clientSession) &&
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

    private void SpawnGmSummonedMonster(int monsterId, PlayerRuntimeState state)
    {
        if (!worldData.MonstersById.TryGetValue(monsterId, out var definition))
            return;

        if (!TryFindFreeGmSummonSlot(out var serverIndex))
            return;

        var monster = MonsterEntity.Create(serverIndex, NextMonsterUniqueNumber(), definition.Monster, serverIndex,
            state.PosX, state.PosY, state.PosZ);

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

    private static MonsterReplicationResponse BuildMonsterActionRecv(MonsterEntity monster,
        int checkChangeActionState)
    {
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
