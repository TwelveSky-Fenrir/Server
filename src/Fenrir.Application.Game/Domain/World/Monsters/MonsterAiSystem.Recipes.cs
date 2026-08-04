using System.Buffers.Binary;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Domain.World.Monsters;

public sealed partial class MonsterAiSystem
{
    private const int StoneIdleResetLegacyTicks = 60 * 120;

    private const int TribeSymbolIdleResetEventCode = 43;

    private const int AllianceStoneIdleResetEventCode = 50;

    private const byte MonsterSymbolRespawnLockSpecialType = 15;

    private const float ThrowCarInnerRadiusRatio = 0.25f;

    private const int ThrowerWanderRollSpan = 100;

    private const int ThrowerWanderProximityCellTolerance = 2;

    private const int ThrowCarDetectionCellTolerance = 2;

    private const int Zone175BossAggroCapacity = 50;

    private const int GuardDetectionCellTolerance = 2;

    private static readonly TimeSpan CarThrowerDetectionCheckThrottle = TimeSpan.FromMilliseconds(100);

    private void RunTribeSymbolStoneDecision(MonsterEntity monster, int legacyTicksElapsed)
    {
        if (!monster.TribeSymbolFirstAttackArmed)
            return;

        monster.TribeSymbolFirstAttackElapsedLegacyTicks = Math.Min(StoneIdleResetLegacyTicks,
            monster.TribeSymbolFirstAttackElapsedLegacyTicks + legacyTicksElapsed);
        if (monster.TribeSymbolFirstAttackElapsedLegacyTicks < StoneIdleResetLegacyTicks)
            return;

        if (monster.Template.SpecialType == MonsterSymbolRespawnLockSpecialType)
            return;

        PublishStoneIdleReset(TribeSymbolIdleResetEventCode, TribeSymbolSlotOf(monster.Template.SpecialType));

        monster.TribeSymbolFirstAttackArmed = false;
        monster.TribeSymbolFirstAttackElapsedLegacyTicks = 0;
        monster.ResetTribeSymbolDamage();
        monster.RestoreFullLife();
    }

    private void RunAllianceStoneDecision(MonsterEntity monster, int legacyTicksElapsed)
    {
        if (!monster.AllianceStoneFirstAttackArmed)
            return;

        monster.AllianceStoneFirstAttackElapsedLegacyTicks = Math.Min(StoneIdleResetLegacyTicks,
            monster.AllianceStoneFirstAttackElapsedLegacyTicks + legacyTicksElapsed);
        if (monster.AllianceStoneFirstAttackElapsedLegacyTicks < StoneIdleResetLegacyTicks)
            return;

        PublishStoneIdleReset(AllianceStoneIdleResetEventCode, AllianceStoneSlotOf(monster.Template.SpecialType));

        monster.AllianceStoneFirstAttackArmed = false;
        monster.AllianceStoneFirstAttackElapsedLegacyTicks = 0;
        monster.RestoreFullLife();
    }

    private static int TribeSymbolSlotOf(byte specialType)
    {
        return specialType switch
        {
            11 => 0,
            12 => 1,
            13 => 2,
            28 => 3,
            14 => 4,
            _ => 0
        };
    }

    private static int AllianceStoneSlotOf(byte specialType)
    {
        return specialType switch
        {
            31 => 0,
            32 => 1,
            33 => 2,
            34 => 3,
            _ => 0
        };
    }

    private static bool TryResolveCarThrowerOwnerTribe(byte specialType, out byte? ownerTribe)
    {
        switch (specialType)
        {
            case 35:
                ownerTribe = 0;
                return true;
            case 36:
                ownerTribe = 1;
                return true;
            case 37:
                ownerTribe = 2;
                return true;
            case 38:
                ownerTribe = 3;
                return true;
            case 18:
                ownerTribe = null;
                return true;
            default:
                ownerTribe = null;
                return false;
        }
    }

    private void PublishStoneIdleReset(int eventCode, int slotIndex)
    {
        if (_siegeIngestor is null)
            return;

        Span<byte> payload = stackalloc byte[ZoneCenterBroadcastIngestor.PayloadSize];
        payload.Clear();
        BinaryPrimitives.WriteInt32LittleEndian(payload, slotIndex);

        _siegeIngestor.Value.Ingest(eventCode, payload);
    }

    private void RunThrowerDecision(Zone zone, MonsterEntity monster)
    {
        if (monster.Template.WalkSpeed >= 1 && _random.NextInt32(ThrowerWanderRollSpan) == 0 &&
            HasNearbyReadyPlayer(zone, monster))
        {
            if (!TryComputeWanderDestination(zone, monster, out var destX, out var destZ))
                return;

            monster.WanderTargetX = destX;
            monster.WanderTargetZ = destZ;
            monster.TargetLocationX = destX;
            monster.TargetLocationY = monster.PosY;
            monster.TargetLocationZ = destZ;
            monster.AiState = MonsterAiState.Patrol;
            monster.StateTicks = 0;
            zone.BroadcastMonsterActionChange(monster);
            return;
        }

        TryThrowCarAcquire(zone, monster);
    }

    private static bool HasNearbyReadyPlayer(Zone zone, MonsterEntity monster)
    {
        var cellSize = zone.AoiCellSize;
        var monsterCellX = (int)(monster.PosX / cellSize);
        var monsterCellY = (int)(monster.PosY / cellSize);
        var monsterCellZ = (int)(monster.PosZ / cellSize);

        foreach (var player in zone.Players)
        {
            if (player.Session is not IZoneSession { State: ZoneSessionState.InWorld })
                continue;

            if (player.IsMovingZone || IsHiding(player))
                continue;

            if (monster.InstanceId is { } requiredInstanceId && player.DungeonInstanceId != requiredInstanceId)
                continue;

            if (Math.Abs((int)(player.PosX / cellSize) - monsterCellX) > ThrowerWanderProximityCellTolerance)
                continue;

            if (Math.Abs((int)(player.PosY / cellSize) - monsterCellY) > ThrowerWanderProximityCellTolerance)
                continue;

            if (Math.Abs((int)(player.PosZ / cellSize) - monsterCellZ) > ThrowerWanderProximityCellTolerance)
                continue;

            return true;
        }

        return false;
    }

    private void TryThrowCarAcquire(Zone zone, MonsterEntity monster)
    {
        var now = DateTime.UtcNow;
        if (monster.LastCarThrowerDetectionCheckAtUtc is { } lastCheck &&
            now - lastCheck < CarThrowerDetectionCheckThrottle)
            return;

        monster.LastCarThrowerDetectionCheckAtUtc = now;

        if (monster.Template.AttackType is not (1 or 3 or 6))
            return;

        var meleeRadius = monster.Template.RadiusInfo1;
        if (meleeRadius <= 0)
            return;

        if (!TryResolveCarThrowerOwnerTribe(monster.Template.SpecialType, out var ownerTribe))
            return;

        var alliedTribe = ownerTribe is { } owned ? _worldState?.GetAllyOf(owned) : null;

        var innerBand = ThrowCarInnerRadiusRatio * meleeRadius;

        var cellSize = zone.AoiCellSize;
        var monsterCellX = (int)(monster.PosX / cellSize);
        var monsterCellY = (int)(monster.PosY / cellSize);
        var monsterCellZ = (int)(monster.PosZ / cellSize);

        foreach (var characterId in StableNeighborsOfPosition(zone, monster.PosX, monster.PosZ,
                     ThrowCarDetectionCellTolerance))
        {
            if (!zone.TryGetPlayer(characterId, out var player) || !IsCandidateValid(player))
                continue;

            if (ownerTribe is { } tribe &&
                (player.Tribe == tribe || (alliedTribe is { } ally && player.Tribe == ally)))
                continue;

            if (player.ActionSort is 0 or 33)
                continue;

            if (monster.InstanceId is { } requiredInstanceId && player.DungeonInstanceId != requiredInstanceId)
                continue;

            if (Math.Abs((int)(player.PosX / cellSize) - monsterCellX) > ThrowCarDetectionCellTolerance ||
                Math.Abs((int)(player.PosY / cellSize) - monsterCellY) > ThrowCarDetectionCellTolerance ||
                Math.Abs((int)(player.PosZ / cellSize) - monsterCellZ) > ThrowCarDetectionCellTolerance)
                continue;

            var length = Distance3D(monster, player);
            if (length < innerBand || length > meleeRadius)
                continue;

            if (_random.NextInt32(2) != 0)
                continue;

            monster.AssignTarget(characterId, player.Incarnation, player.UniqueNumber, player.PosX, player.PosY,
                player.PosZ);
            monster.RegisterAcquisition(characterId, player.Incarnation);
            monster.ArmAttackPacketConfirmation();
            monster.AiState = MonsterAiState.AttackWindup;
            monster.StateTicks = 0;
            zone.BroadcastMonsterActionChange(monster);
            return;
        }
    }

    private void RunZone175BossDecision(Zone zone, MonsterEntity monster, int legacyTicksElapsed)
    {
        monster.DetectionThrottleTicks += legacyTicksElapsed;
        if (monster.DetectionThrottleTicks < SimulationClock.MonsterDetectionThrottleLegacyTicks)
            return;

        monster.DetectionThrottleTicks = 0;

        var wideRadiusSq = (float)monster.Template.RadiusInfo2 * monster.Template.RadiusInfo2;
        var meleeRadiusSq = (float)monster.Template.RadiusInfo1 * monster.Template.RadiusInfo1;

        var aggro = AcquireZone175BossCandidates(zone, monster, wideRadiusSq);
        if (aggro.Count == 0)
            return;

        if (_random.NextInt32(3) == 0)
        {
            if (TryPickBossTarget(aggro, meleeRadiusSq, true, out var far))
                CommitBossTarget(zone, monster, far, MonsterAiState.RangedAttackWindup);

            return;
        }

        if (!TryPickBossTarget(aggro, meleeRadiusSq, false, out var near))
            near = aggro[_random.NextInt32(aggro.Count)];

        CommitBossTarget(zone, monster, near,
            near.DistanceSquared <= meleeRadiusSq ? MonsterAiState.AttackWindup : MonsterAiState.Chase);
    }

    private List<MonsterAggroCandidate> AcquireZone175BossCandidates(Zone zone, MonsterEntity monster,
        float wideRadiusSq)
    {
        var aggro = zone.BorrowBossAggroScratch();

        foreach (var player in zone.Players)
        {
            if (!IsCandidateValid(player))
                continue;

            if (player.ActionSort is 0 or 33)
                continue;

            if (monster.InstanceId is { } requiredInstanceId && player.DungeonInstanceId != requiredInstanceId)
                continue;

            var distanceSq = DistanceSquared(monster.PosX, monster.PosZ, player.PosX, player.PosZ);
            if (distanceSq > wideRadiusSq)
                continue;

            if (_random.NextInt32(2) != 0)
                continue;

            monster.RegisterAcquisition(player.CharacterId, player.Incarnation);
            aggro.Add(new MonsterAggroCandidate(player.CharacterId, player.Incarnation, player.UniqueNumber, distanceSq,
                player.PosX, player.PosY, player.PosZ));

            if (aggro.Count >= Zone175BossAggroCapacity)
                break;
        }

        return aggro;
    }

    private bool TryPickBossTarget(List<MonsterAggroCandidate> aggro, float meleeRadiusSq, bool wantFar,
        out MonsterAggroCandidate picked)
    {
        foreach (var candidate in aggro)
        {
            var matches = wantFar
                ? candidate.DistanceSquared >= meleeRadiusSq
                : candidate.DistanceSquared <= meleeRadiusSq;
            if (!matches)
                continue;

            if (_random.NextInt32(2) != 0)
                continue;

            picked = candidate;
            return true;
        }

        picked = default;
        return false;
    }

    private static void CommitBossTarget(Zone zone, MonsterEntity monster, MonsterAggroCandidate target,
        MonsterAiState nextState)
    {
        monster.AssignTarget(target.CharacterId, target.Incarnation, target.UniqueNumber, target.PosX, target.PosY,
            target.PosZ);
        if (nextState is MonsterAiState.AttackWindup or MonsterAiState.RangedAttackWindup)
            monster.ArmAttackPacketConfirmation();
        monster.AiState = nextState;
        monster.StateTicks = 0;
        zone.BroadcastMonsterActionChange(monster);
    }

    private static float Distance3D(MonsterEntity monster, PlayerRuntimeState player)
    {
        var dx = monster.PosX - player.PosX;
        var dy = monster.PosY - player.PosY;
        var dz = monster.PosZ - player.PosZ;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private void RunGuardDecision(Zone zone, MonsterEntity monster, int legacyTicksElapsed)
    {
        monster.DetectionThrottleTicks += legacyTicksElapsed;
        if (monster.DetectionThrottleTicks < SimulationClock.MonsterDetectionThrottleLegacyTicks)
            return;

        monster.DetectionThrottleTicks = 0;

        var meleeRadius = monster.Template.RadiusInfo1;
        if (meleeRadius <= 0)
            return;

        var guardTribe = monster.Template.Type switch
        {
            6 => (byte)0,
            7 => (byte)1,
            8 => (byte)2,
            9 => (byte)3,
            _ => (byte?)null
        };
        if (guardTribe is not { } tribe)
            return;

        var alliedTribe = _worldState?.GetAllyOf(tribe);

        var meleeRadiusSq = (float)meleeRadius * meleeRadius;

        var cellSize = zone.AoiCellSize;
        var monsterCellX = (int)(monster.PosX / cellSize);
        var monsterCellY = (int)(monster.PosY / cellSize);
        var monsterCellZ = (int)(monster.PosZ / cellSize);

        foreach (var characterId in StableNeighborsOfPosition(zone, monster.PosX, monster.PosZ,
                     GuardDetectionCellTolerance))
        {
            if (!zone.TryGetPlayer(characterId, out var player) || !IsCandidateValid(player))
                continue;

            if (player.Tribe == tribe || (alliedTribe is { } ally && player.Tribe == ally))
                continue;

            if (player.ActionSort is 0 or 33)
                continue;

            if (Math.Abs((int)(player.PosX / cellSize) - monsterCellX) > GuardDetectionCellTolerance ||
                Math.Abs((int)(player.PosY / cellSize) - monsterCellY) > GuardDetectionCellTolerance ||
                Math.Abs((int)(player.PosZ / cellSize) - monsterCellZ) > GuardDetectionCellTolerance)
                continue;

            if (DistanceSquared(monster.PosX, monster.PosZ, player.PosX, player.PosZ) > meleeRadiusSq)
                continue;

            if (_random.NextInt32(2) != 0)
                continue;

            monster.AssignTarget(characterId, player.Incarnation, player.UniqueNumber, player.PosX, player.PosY,
                player.PosZ);
            monster.RegisterAcquisition(characterId, player.Incarnation);
            monster.ArmAttackPacketConfirmation();
            monster.AiState = MonsterAiState.AttackWindup;
            monster.StateTicks = 0;
            zone.BroadcastMonsterActionChange(monster);
            return;
        }
    }
}
