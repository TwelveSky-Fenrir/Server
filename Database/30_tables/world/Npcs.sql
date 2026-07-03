-- One row per NPC_INFO record (Header/Protocol/STRUCT.h:213-234, 005_00005.IMG) whose legacy Index is
-- nonzero -- 369 of the 500 array slots are unused placeholders (Index==0 AND Name=="" for every single
-- one of them, confirmed 1:1 equivalent by inspecting the real decoded data, not assumed the way items'
-- index-0 filtering was) -- leaving 131 real NPCs, Index range 1..424.
-- NpcId is the legacy nIndex, never re-assigned -- part of the cross-domain PK contract other domains'
-- FKs rely on (e.g. world.ZoneNpcSpawns.NpcId, designed in the parallel zone-topology domain).
-- SpeechNum (legacy nSpeechNum, a count of populated outer Speech groups) is deliberately NOT a column
-- here: it is fully derivable from COUNT(DISTINCT SpeechGroup) over world.NpcSpeeches and persisting it
-- separately would just be a driftable copy of that same fact.
-- Size1/2/3 (legacy nSize[3]) are kept as three plain columns, not a child table: a real always-present
-- 3-slot bounding-box/collision-size triple, not a sparse list -- exactly the "small, always-meaningful
-- fixed array" case the normalization rule carves out. Its per-axis meaning (width/height/radius?) is not
-- confirmed by prior investigation, so the columns are numbered rather than guessed at (e.g. not
-- SizeX/SizeY/SizeZ, which would imply a spatial-axis meaning nothing here confirms).
CREATE TABLE world.Npcs
(
    NpcId            INT     NOT NULL,
    Name             NVARCHAR(28) NOT NULL, -- legacy nName buffer is 28 bytes; longest real value observed is 23 chars
    Tribe            TINYINT NOT NULL,      -- legacy nTribe; observed range 1-5 among real NPCs (0 only ever appears on unused slots)
    Type             TINYINT NOT NULL,      -- legacy nType; observed range 1-17 among real NPCs (0 only ever appears on unused slots)
    DataSortNumber2D INT     NOT NULL,
    DataSortNumber3D INT     NOT NULL,
    Size1            INT     NOT NULL,
    Size2            INT     NOT NULL,
    Size3            INT     NOT NULL,
    CONSTRAINT PK_Npcs PRIMARY KEY CLUSTERED (NpcId)
);
