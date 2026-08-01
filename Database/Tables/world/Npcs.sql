-- Legacy NPC_INFO; one row per record where Index != 0 (369 of 500 slots are unused placeholders). NpcId is the legacy nIndex, the PK other domains' FKs rely on (e.g. world.ZoneNpcSpawns.NpcId).
-- Size1/2/3 (nSize[3]) per-axis meaning (width/height/radius?) is unconfirmed, so columns stay numbered rather than named.
-- The CHECKs below mirror legacy's load-time field validation (Npc_CheckValidElement,
-- Server/Header/S15_MyShare.cpp:1836-1963, bounds at Server/Header/Protocol/STRUCT.h:206-235), which rejects
-- the whole 500-slot Load_Npc call on the first offending record -- fatal to both ts25sharemem's and
-- ts25zone's boot sequence. A relational CHECK is the closest Fenrir equivalent of "this value can never be
-- stored" (stricter in one respect: enforced on every write, not just one boot-time pass).
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
    CONSTRAINT PK_Npcs PRIMARY KEY CLUSTERED (NpcId),
    CONSTRAINT CK_Npcs_Tribe CHECK (Tribe BETWEEN 1 AND 5),
    CONSTRAINT CK_Npcs_Type CHECK (Type BETWEEN 1 AND 17),
    CONSTRAINT CK_Npcs_DataSortNumber2D CHECK (DataSortNumber2D BETWEEN 1 AND 10000),
    CONSTRAINT CK_Npcs_DataSortNumber3D CHECK (DataSortNumber3D BETWEEN 1 AND 10000),
    CONSTRAINT CK_Npcs_Size1 CHECK (Size1 BETWEEN 1 AND 1000),
    CONSTRAINT CK_Npcs_Size2 CHECK (Size2 BETWEEN 1 AND 1000),
    CONSTRAINT CK_Npcs_Size3 CHECK (Size3 BETWEEN 1 AND 1000)
);
