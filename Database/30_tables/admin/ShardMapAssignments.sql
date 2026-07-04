-- ADR-0012: a shard hosts a disjoint partition of maps -- UQ on MapId enforces that invariant directly.
CREATE TABLE admin.ShardMapAssignments
(
    ShardId TINYINT  NOT NULL,
    MapId   SMALLINT NOT NULL,
    CONSTRAINT PK_ShardMapAssignments PRIMARY KEY CLUSTERED (ShardId, MapId),
    CONSTRAINT UQ_ShardMapAssignments_MapId UNIQUE (MapId)
);
