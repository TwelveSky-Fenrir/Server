CREATE TABLE admin.ShardMapAssignments
(
    ShardId TINYINT  NOT NULL,
    MapId   SMALLINT NOT NULL,
    CONSTRAINT PK_ShardMapAssignments PRIMARY KEY CLUSTERED (ShardId, MapId),
    CONSTRAINT UQ_ShardMapAssignments_MapId UNIQUE (MapId),
    CONSTRAINT FK_ShardMapAssignments_World_Zone FOREIGN KEY (MapId) REFERENCES world.Zones (ZoneNumber)
);
