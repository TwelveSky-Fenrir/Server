using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Domain.World.Monsters;

public static class MonsterAggroListPruner
{
    private const int TrackedAttackerCellTolerance = 1;

    public static Result Prune(Zone zone, MonsterEntity monster, IEnumerable<MonsterEntity> allMonsters,
        List<Survivor>? resultBuffer = null)
    {
        var survivors = resultBuffer ?? [];
        survivors.Clear();

        var meleeRadius = monster.Template.RadiusInfo1;
        var leashRadius = monster.Template.RadiusInfo2;

        if (meleeRadius <= 0 || leashRadius <= 0)
            return new Result(survivors);

        var meleeRadiusSq = (float)meleeRadius * meleeRadius;
        var leashRadiusSq = (float)leashRadius * leashRadius;

        var cellSize = zone.AoiCellSize;
        var monsterCellX = (int)(monster.PosX / cellSize);
        var monsterCellY = (int)(monster.PosY / cellSize);
        var monsterCellZ = (int)(monster.PosZ / cellSize);

        foreach (var entry in monster.SnapshotAttackDamage())
        {
            if (!TryEvaluateEntry(zone, monster, allMonsters, entry, meleeRadiusSq, leashRadiusSq,
                    cellSize, monsterCellX, monsterCellY, monsterCellZ, out var distanceSquared))
                continue;

            survivors.Add(new Survivor(entry.CharacterId, entry.SessionToken, entry.CumulativeDamage,
                distanceSquared));
        }

        return new Result(survivors);
    }

    private static bool TryEvaluateEntry(Zone zone, MonsterEntity monster, IEnumerable<MonsterEntity> allMonsters,
        MonsterAttackDamageEntry entry, float meleeRadiusSq, float leashRadiusSq,
        float cellSize, int monsterCellX, int monsterCellY, int monsterCellZ, out float distanceSquared)
    {
        distanceSquared = 0f;

        if (!zone.TryGetPlayer(entry.CharacterId, out var player) || player is null ||
            !ReferenceEquals(player, entry.SessionToken))
            return false;

        if (player.Session is not IZoneSession { State: ZoneSessionState.InWorld })
            return false;

        if (player.IsMovingZone || IsHiding(player) || player.IsDead)
            return false;

        if (player.ActionSort is 0 or 33)
            return false;

        if (Math.Abs((int)(player.PosX / cellSize) - monsterCellX) > TrackedAttackerCellTolerance ||
            Math.Abs((int)(player.PosY / cellSize) - monsterCellY) > TrackedAttackerCellTolerance ||
            Math.Abs((int)(player.PosZ / cellSize) - monsterCellZ) > TrackedAttackerCellTolerance)
            return false;

        distanceSquared = DistanceSquared(monster.PosX, monster.PosZ, player.PosX, player.PosZ);

        if (distanceSquared > leashRadiusSq)
            return false;

        if (distanceSquared > meleeRadiusSq)
        {
            var otherPursuers = CountOtherPursuers(allMonsters, monster, entry.CharacterId);
            return otherPursuers < monster.PursuerCapacity;
        }

        return MathF.Abs(player.PosY - monster.PosY) <= monster.Template.Size2;
    }

    private static bool IsHiding(PlayerRuntimeState player)
    {
        return player.VisibleState == 0;
    }

    private static int CountOtherPursuers(IEnumerable<MonsterEntity> allMonsters, MonsterEntity monster,
        int candidateCharacterId)
    {
        var count = 0;
        foreach (var other in allMonsters)
        {
            if (other.ServerIndex == monster.ServerIndex)
                continue;

            if (other.AiState is not (MonsterAiState.Chase or MonsterAiState.AttackWindup
                or MonsterAiState.RangedAttackWindup))
                continue;

            if (other.TargetCharacterId == candidateCharacterId)
                count++;
        }

        return count;
    }

    private static float DistanceSquared(float x1, float z1, float x2, float z2)
    {
        var dx = x1 - x2;
        var dz = z1 - z2;
        return dx * dx + dz * dz;
    }

    public readonly record struct Survivor(
        int CharacterId,
        object SessionToken,
        long CumulativeDamage,
        float DistanceSquared);

    public readonly record struct Result(IReadOnlyList<Survivor> Survivors)
    {
        public bool HasValidAttackers => Survivors.Count > 0;
    }
}
