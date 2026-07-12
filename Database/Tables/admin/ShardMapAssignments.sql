-- ADR-0012: a shard hosts a disjoint partition of maps -- UQ on MapId enforces that invariant directly.
-- MapId is the same legacy zone/map number as world.Zones.ZoneNumber -- FK'd below so a typo'd or
-- retired map can never be assigned to a shard in the first place, rather than only being caught later
-- by ShardPartitionGuard.EnsureNoOverlapAsync at boot.
CREATE TABLE admin.ShardMapAssignments
(
    ShardId TINYINT  NOT NULL,
    MapId   SMALLINT NOT NULL,
    CONSTRAINT PK_ShardMapAssignments PRIMARY KEY CLUSTERED (ShardId, MapId),
    CONSTRAINT UQ_ShardMapAssignments_MapId UNIQUE (MapId),
    -- Cross-schema FK naming: see admin.Bans' own header comment for the FK_<ChildTable>_<TargetSchema>_<Role>
    -- convention.
    CONSTRAINT FK_ShardMapAssignments_World_Zone FOREIGN KEY (MapId) REFERENCES world.Zones (ZoneNumber)
);
