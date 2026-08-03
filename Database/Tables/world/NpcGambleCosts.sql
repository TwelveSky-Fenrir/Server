CREATE TABLE world.NpcGambleCosts
(
    NpcId      INT      NOT NULL,
    GambleTier SMALLINT NOT NULL,
    CostIndex  TINYINT  NOT NULL,
    Value      INT      NOT NULL,
    CONSTRAINT PK_NpcGambleCosts PRIMARY KEY CLUSTERED (NpcId, GambleTier, CostIndex),
    CONSTRAINT CK_NpcGambleCosts_GambleTier CHECK (GambleTier BETWEEN 0 AND 144),
    CONSTRAINT CK_NpcGambleCosts_CostIndex CHECK (CostIndex BETWEEN 0 AND 14),
    CONSTRAINT FK_NpcGambleCosts_Npcs FOREIGN KEY (NpcId) REFERENCES world.Npcs (NpcId),
    CONSTRAINT CK_NpcGambleCosts_Value CHECK (Value BETWEEN 0 AND 100000000)
);
