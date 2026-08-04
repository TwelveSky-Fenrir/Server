using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.World.Runtime;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Domain.World.Monsters;

public static class MonsterAggroListPruner
{
    private const int TrackedAttackerCellTolerance = 1;

    public static Result Prune(Zone zone, MonsterEntity monster, List<Survivor>? resultBuffer = null)
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
            if (!TryEvaluateEntry(zone, monster, entry, meleeRadiusSq, leashRadiusSq,
                    cellSize, monsterCellX, monsterCellY, monsterCellZ, out var distanceSquared))
                continue;

            survivors.Add(new Survivor(entry.CharacterId, entry.Incarnation, entry.CumulativeDamage,
                distanceSquared));
        }

        return new Result(survivors);
    }

    private static bool TryEvaluateEntry(Zone zone, MonsterEntity monster, MonsterAttackDamageEntry entry,
        float meleeRadiusSq, float leashRadiusSq,
        float cellSize, int monsterCellX, int monsterCellY, int monsterCellZ, out float distanceSquared)
    {
        distanceSquared = 0f;

        if (!zone.TryGetPlayer(entry.CharacterId, out var player) || player is null ||
            player.Incarnation != entry.Incarnation)
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
            var otherPursuers = zone.CountOtherMonsterPursuers(monster, entry.CharacterId, entry.Incarnation);
            return otherPursuers < monster.PursuerCapacity;
        }

        return MathF.Abs(player.PosY - monster.PosY) <= monster.Template.Size2;
    }

    private static bool IsHiding(PlayerRuntimeState player)
    {
        return player.VisibleState == 0;
    }

    private static float DistanceSquared(float x1, float z1, float x2, float z2)
    {
        var dx = x1 - x2;
        var dz = z1 - z2;
        return dx * dx + dz * dz;
    }

    public readonly record struct Survivor(
        int CharacterId,
        RuntimeIncarnation Incarnation,
        long CumulativeDamage,
        float DistanceSquared);

    public readonly record struct Result(IReadOnlyList<Survivor> Survivors)
    {
        public bool HasValidAttackers => Survivors.Count > 0;
    }
}
