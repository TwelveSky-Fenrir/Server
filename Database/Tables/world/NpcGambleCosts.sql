-- Normalized from nGambleCostInfo[145][15]; sparse only at the NPC level (used by 6 "Beggar" NPCs) -- once an NPC uses it, all 2175 cells are populated, unlike NpcShopItems/NpcSkillOffers which are also sparse within-NPC.
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
    -- Value mirrors legacy's own load-time bound (Npc_CheckValidElement, Server/Header/S15_MyShare.cpp:1953-1962).
    CONSTRAINT CK_NpcGambleCosts_Value CHECK (Value BETWEEN 0 AND 100000000)
);
