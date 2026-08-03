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
