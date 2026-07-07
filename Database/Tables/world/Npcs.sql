-- Legacy NPC_INFO; one row per record where Index != 0 (369 of 500 slots are unused placeholders). NpcId is the legacy nIndex, the PK other domains' FKs rely on (e.g. world.ZoneNpcSpawns.NpcId).
-- Size1/2/3 (nSize[3]) per-axis meaning (width/height/radius?) is unconfirmed, so columns stay numbered rather than named.
CREATE TABLE world.Npcs
(
    NpcId            INT          NOT NULL,
    Name             NVARCHAR(28) NOT NULL,
    Tribe            TINYINT      NOT NULL,
    Type             TINYINT      NOT NULL,
    DataSortNumber2D INT          NOT NULL,
    DataSortNumber3D INT          NOT NULL,
    Size1            INT          NOT NULL,
    Size2            INT          NOT NULL,
    Size3            INT          NOT NULL,
    CONSTRAINT PK_Npcs PRIMARY KEY CLUSTERED (NpcId)
);
