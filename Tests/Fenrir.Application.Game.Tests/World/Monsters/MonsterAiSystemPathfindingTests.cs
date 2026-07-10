using System.Numerics;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Geometry;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Tests.GameData;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World.Monsters;

/// <summary>
///     Covers <see cref="MonsterAiSystem" />'s A* + funnel pathfinding integration (workstream A2): a chasing
///     monster on loaded geometry follows a navmesh route (ground-snapping as it moves), routes <em>around</em>
///     an obstacle instead of stalling at a blocked step, degrades to a straight-line step when the per-tick
///     path budget is exhausted, and behaves exactly as before (unconstrained straight line, no ground snap)
///     when no geometry is loaded.
/// </summary>
public class MonsterAiSystemPathfindingTests
{
    private const float GroundY = 10f;

    private static WorldDataCache CacheWithRegionAt(int locX, int locZ, short radiusInfo1, short radiusInfo2,
        short runSpeed)
    {
        var monster = WorldDataTestRows.Monster(600) with
        {
            Life = 1000,
            ItemLevel = 1,
            RealLevel = 1,
            SummonTime1 = 9999,
            SummonTime2 = 9999,
            FrameInfo1 = 1,
            FrameInfo3 = 1,
            RadiusInfo1 = radiusInfo1,
            RadiusInfo2 = radiusInfo2,
            WalkSpeed = runSpeed,
            RunSpeed = runSpeed,
            AttackType = 1
        };
        var region = WorldDataTestRows.SpawnRegion(1, 1, 600) with
        {
            Number = 1,
            LocationX = locX,
            LocationY = 0,
            LocationZ = locZ,
            Radius = 0
        };

        var rows = WorldDataTestRows.MinimalRows() with { Monsters = [monster], MonsterSpawnRegions = [region] };
        return WorldDataCacheBuilder.Build(rows).Cache;
    }

    private static Zone CreateZone(WorldDataCache cache, ZoneGeometry? geometry, GameServerOptions? options = null)
    {
        var scheduler = new MonsterSpawnScheduler(cache, static () => new ZeroScatterRandom());
        // ScriptedRandomSource(0) resolves the detection coin flip to a guaranteed success (see MonsterAiSystemTests).
        var ai = new MonsterAiSystem(new ScriptedRandomSource(0));
        var opts = options ?? new GameServerOptions { AoiCellSize = 100_000f };
        opts.AoiCellSize = 100_000f;
        return ZoneTestKit.CreateZone(1, opts, simulationSystems: [scheduler, ai], worldData: cache,
            geometry: geometry);
    }

    /// <summary>A large flat walkable square at <see cref="GroundY" />, split into two triangles.</summary>
    private static ZoneGeometry FlatGeometry()
    {
        var plane = new Vector4(0f, 1f, 0f, GroundY);
        var triangles = new[]
        {
            new WorldTriangle(new Vector3(-1000, GroundY, -1000), new Vector3(1000, GroundY, -1000),
                new Vector3(1000, GroundY, 1000), plane),
            new WorldTriangle(new Vector3(-1000, GroundY, -1000), new Vector3(1000, GroundY, 1000),
                new Vector3(-1000, GroundY, 1000), plane)
        };
        var root = new QuadtreeNode(new Vector3(-1000, 0, -1000), new Vector3(1000, GroundY, 1000), [0, 1],
            [-1, -1, -1, -1]);
        return new ZoneGeometry(triangles, [root]);
    }

    /// <summary>
    ///     Grid navmesh (10-unit diagonal-split cells) of two columns joined only by a top connector, with a
    ///     two-cell-tall gap between the columns' bottoms -- a straight line from (7,3) to (27,3) is blocked and
    ///     must detour up and over. Mirrors <c>MonsterPathfinderTests.GapWithTopDetour</c>.
    /// </summary>
    private static ZoneGeometry GapGeometry()
    {
        (int Col, int Row)[] cells =
        [
            (0, 0), (0, 1), (0, 2),
            (2, 0), (2, 1), (2, 2),
            (1, 2)
        ];

        const float cellSize = 10f;
        var triangles = new List<WorldTriangle>(cells.Length * 2);
        var plane = new Vector4(0f, 1f, 0f, GroundY);

        foreach (var (col, row) in cells)
        {
            var x0 = col * cellSize;
            var x1 = (col + 1) * cellSize;
            var z0 = row * cellSize;
            var z1 = (row + 1) * cellSize;
            var v00 = new Vector3(x0, GroundY, z0);
            var v10 = new Vector3(x1, GroundY, z0);
            var v11 = new Vector3(x1, GroundY, z1);
            var v01 = new Vector3(x0, GroundY, z1);
            triangles.Add(new WorldTriangle(v00, v10, v11, plane));
            triangles.Add(new WorldTriangle(v00, v11, v01, plane));
        }

        var indices = new int[triangles.Count];
        for (var i = 0; i < indices.Length; i++)
            indices[i] = i;
        var root = new QuadtreeNode(new Vector3(0, 0, 0), new Vector3(30, GroundY, 30), indices, [-1, -1, -1, -1]);
        return new ZoneGeometry(triangles.ToArray(), [root]);
    }

    [Fact]
    public void ChasingMonster_OnFlatGeometry_MovesTowardTarget_AndSnapsToGround()
    {
        // Melee range (RadiusInfo1) is tiny and the target is far, so the monster must chase across the mesh.
        var zone = CreateZone(CacheWithRegionAt(0, 0, 2, 1000, 40), FlatGeometry());
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "Target", 200f, posZ: 0f)));

        var moved = false;
        var snapped = false;
        for (var i = 0; i < 20; i++)
        {
            zone.Tick(SimulationClock.LegacyTick);
            Assert.True(zone.TryGetMonster(1, out var monster));
            if (monster!.PosX > 5f)
                moved = true;
            if (moved && MathF.Abs(monster.PosY - GroundY) < 0.001f)
                snapped = true;
        }

        Assert.True(moved, "monster never advanced toward the target on loaded geometry");
        Assert.True(snapped, "monster never snapped to the loaded ground height while pathing");
    }

    [Fact]
    public void ChasingMonster_WithNoGeometry_MovesButNeverSnapsToGround()
    {
        // Same scenario but geometry: null -- unconstrained straight-line (the pre-A2 behavior), so Y stays 0.
        var zone = CreateZone(CacheWithRegionAt(0, 0, 2, 1000, 40), null);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "Target", 200f, posZ: 0f)));

        var moved = false;
        for (var i = 0; i < 20; i++)
        {
            zone.Tick(SimulationClock.LegacyTick);
            Assert.True(zone.TryGetMonster(1, out var monster));
            if (monster!.PosX > 5f)
                moved = true;
            Assert.Equal(0f, monster.PosY); // no geometry -> no ground snap, unchanged from before
        }

        Assert.True(moved, "monster never advanced toward the target without geometry");
    }

    [Fact]
    public void ChasingMonster_AcrossAnObstacle_RoutesAroundIt()
    {
        var zone = CreateZone(CacheWithRegionAt(7, 3, 2, 1000, 10), GapGeometry());
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "Target", 27f, posZ: 3f)));

        var maxZ = float.MinValue;
        for (var i = 0; i < 40; i++)
        {
            zone.Tick(SimulationClock.LegacyTick);
            Assert.True(zone.TryGetMonster(1, out var monster));
            maxZ = MathF.Max(maxZ, monster!.PosZ);

            // It must never stand inside the impassable gap (x in [10,20], z below the top connector at z=20).
            var insideGap = monster.PosX is > 10f and < 20f && monster.PosZ < 20f;
            Assert.False(insideGap,
                $"monster entered the impassable gap at ({monster.PosX:F1}, {monster.PosZ:F1})");
        }

        // Routing up-and-over the two-cell-tall gap drives the monster's z well above its start/target z (=3).
        Assert.True(maxZ >= 12f, $"monster never routed up around the obstacle (max z reached {maxZ:F1})");
    }

    [Fact]
    public void OverBudgetMonster_OnGeometry_FallsBackToStraightLine_AndKeepsMoving()
    {
        // Budget 0: every constrained move degrades to the straight-line-with-refusal fallback. On open flat
        // ground that still advances the monster toward the target and snaps it to ground, without crashing.
        var options = new GameServerOptions { AoiCellSize = 100_000f, MonsterPathfindingBudgetPerTick = 0 };
        var zone = CreateZone(CacheWithRegionAt(0, 0, 2, 1000, 40), FlatGeometry(), options);
        var (session, _) = ZoneTestKit.CreateSession(1);
        zone.Post(ZoneCommand.Enter(10, ZoneTestKit.EnterData(session, 1, "Target", 200f, posZ: 0f)));

        var moved = false;
        var snapped = false;
        for (var i = 0; i < 20; i++)
        {
            zone.Tick(SimulationClock.LegacyTick);
            Assert.True(zone.TryGetMonster(1, out var monster));
            if (monster!.PosX > 5f)
                moved = true;
            if (moved && MathF.Abs(monster.PosY - GroundY) < 0.001f)
                snapped = true;
        }

        Assert.True(moved, "over-budget monster never advanced via the straight-line fallback");
        Assert.True(snapped, "over-budget straight-line fallback never snapped to ground");
    }

    /// <summary>Forces spawn scatter to exactly 0, so a monster spawns AT its region location.</summary>
    private sealed class ZeroScatterRandom : Random
    {
        public override double NextDouble()
        {
            return 0;
        }
    }
}
