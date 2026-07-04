-- Static shard/map topology (ADR-0012: a shard hosts a DISJOINT partition of maps). Replaces
-- Game:Maps (appsettings/env config) as the source of truth GameServer reads at boot -- repartitioning
-- shards becomes an UPDATE here, not a redeploy.
-- UQ on MapId is the disjointness invariant itself: a map can only be assigned to one shard, so two
-- shards racing to host the same map is a constraint violation, not just a documented convention.
CREATE TABLE admin.ShardMapAssignments
(
    ShardId TINYINT  NOT NULL,
    MapId   SMALLINT NOT NULL,
    CONSTRAINT PK_ShardMapAssignments PRIMARY KEY CLUSTERED (ShardId, MapId),
    CONSTRAINT UQ_ShardMapAssignments_MapId UNIQUE (MapId)
);
