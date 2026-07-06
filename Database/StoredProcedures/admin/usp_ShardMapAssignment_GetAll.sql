-- Liveness-independent complement to usp_ShardMapAssignment_GetForShard: returns every (ShardId, MapId) row
-- in admin.ShardMapAssignments regardless of which shards are currently live in runtime.GameServerDirectory.
-- ShardPartitionGuard.EnsureNoOverlapAsync needs this so a boot-time map-overlap check does not depend on
-- every colliding shard having already heartbeated -- e.g. a whole fleet cold-booting at the same instant.
CREATE PROCEDURE admin.usp_ShardMapAssignment_GetAll
AS
BEGIN
    SET NOCOUNT ON;

    SELECT ShardId, MapId
    FROM admin.ShardMapAssignments
    ORDER BY ShardId, MapId;
END;
