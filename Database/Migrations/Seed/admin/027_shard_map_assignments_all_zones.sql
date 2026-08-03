INSERT INTO admin.ShardMapAssignments (ShardId, MapId)
SELECT 1, z.ZoneNumber
FROM world.Zones AS z
WHERE NOT EXISTS (SELECT 1
                  FROM admin.ShardMapAssignments AS a
                  WHERE a.MapId = z.ZoneNumber);
