using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Wire;

namespace Fenrir.Application.Game.Domain.World.Monsters;

public sealed partial class MonsterAiSystem
{
    private const float ThrowCarInnerRadiusRatio = 0.25f;

    private const int ThrowerWanderRollSpan = 100;

    private const int ThrowerWanderProximityCellTolerance = 2;

    private const int Zone175BossAggroCapacity = 50;

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
        var monsterCellX = MathF.Floor(monster.PosX / cellSize);
        var monsterCellY = MathF.Floor(monster.PosY / cellSize);
        var monsterCellZ = MathF.Floor(monster.PosZ / cellSize);

        foreach (var player in zone.Players)
        {
            if (player.Session is not ZoneClientSession { State: ZoneSessionState.InWorld })
                continue;

            if (player.IsMovingZone || IsHiding(player))
                continue;

            if (monster.InstanceId is { } requiredInstanceId && player.DungeonInstanceId != requiredInstanceId)
                continue;

            if (MathF.Abs(MathF.Floor(player.PosX / cellSize) - monsterCellX) > ThrowerWanderProximityCellTolerance)
                continue;

            if (MathF.Abs(MathF.Floor(player.PosY / cellSize) - monsterCellY) > ThrowerWanderProximityCellTolerance)
                continue;

            if (MathF.Abs(MathF.Floor(player.PosZ / cellSize) - monsterCellZ) > ThrowerWanderProximityCellTolerance)
                continue;

            return true;
        }

        return false;
    }

    private void TryThrowCarAcquire(Zone zone, MonsterEntity monster)
    {
        if (monster.Template.AttackType is not (1 or 3 or 6))
            return;

        var meleeRadius = monster.Template.RadiusInfo1;
        if (meleeRadius <= 0)
            return;

        var innerBand = ThrowCarInnerRadiusRatio * meleeRadius;

        foreach (var characterId in zone.NeighborsOfPosition(monster.PosX, monster.PosZ))
        {
            if (!zone.TryGetPlayer(characterId, out var player) || !IsCandidateValid(player))
                continue;

            if (monster.InstanceId is { } requiredInstanceId && player.DungeonInstanceId != requiredInstanceId)
                continue;

            var length = Distance3D(monster, player);
            if (length < innerBand || length > meleeRadius)
                continue;

            if (_random.NextInt32(2) != 0)
                continue;

            monster.AssignTarget(characterId, player.UniqueNumber, player.PosX, player.PosY, player.PosZ);
            monster.RegisterAcquisition(characterId, player);
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

        var wideRadius = monster.Template.RadiusInfo2;
        if (wideRadius <= 0)
            return;

        var wideRadiusSq = (float)wideRadius * wideRadius;
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

        foreach (var characterId in zone.NeighborsOfPosition(monster.PosX, monster.PosZ))
        {
            if (!zone.TryGetPlayer(characterId, out var player) || !IsCandidateValid(player))
                continue;

            if (monster.InstanceId is { } requiredInstanceId && player.DungeonInstanceId != requiredInstanceId)
                continue;

            var distanceSq = DistanceSquared(monster.PosX, monster.PosZ, player.PosX, player.PosZ);
            if (distanceSq > wideRadiusSq)
                continue;

            if (_random.NextInt32(2) != 0)
                continue;

            monster.RegisterAcquisition(characterId, player);
            aggro.Add(new MonsterAggroCandidate(characterId, player.UniqueNumber, distanceSq, player.PosX,
                player.PosY, player.PosZ));

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
        monster.AssignTarget(target.CharacterId, target.UniqueNumber, target.PosX, target.PosY, target.PosZ);
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

        var meleeRadiusSq = (float)meleeRadius * meleeRadius;

        foreach (var characterId in zone.NeighborsOfPosition(monster.PosX, monster.PosZ))
        {
            if (!zone.TryGetPlayer(characterId, out var player) || !IsCandidateValid(player))
                continue;

            if (DistanceSquared(monster.PosX, monster.PosZ, player.PosX, player.PosZ) > meleeRadiusSq)
                continue;

            if (_random.NextInt32(2) != 0)
                continue;

            monster.AssignTarget(characterId, player.UniqueNumber, player.PosX, player.PosY, player.PosZ);
            monster.RegisterAcquisition(characterId, player);
            monster.AiState = MonsterAiState.AttackWindup;
            monster.StateTicks = 0;
            zone.BroadcastMonsterActionChange(monster);
            return;
        }
    }
}
