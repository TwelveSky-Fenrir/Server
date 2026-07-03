-- Normalized from nGambleCostInfo[145][15] (Header/Protocol/STRUCT.h:213-234): only nonzero cells kept.
-- Sparse BY NPC as expected -- only 6 of the 131 real NPCs (all named "Beggar ...", a dedicated
-- gambling-minigame NPC type) use this feature at all -- but a surprising finding surfaced while
-- generating the real seed data (flagged here rather than silently seeding a huge block unremarked):
-- 0 filtering removes NOTHING once a given NPC uses the feature. Every one of the 145*15=2175 cells is
-- populated for each of those 6 NPCs (13050 rows == 6 * 145 * 15 exactly), unlike NpcShopItems/
-- NpcSkillOffers where sparsity holds at both the NPC level and the within-NPC slot level.
-- GambleTier/CostIndex keep the original 0-based row/column position for traceability even though their
-- exact real-world meaning (145 prize tiers? 15 currency/bet-amount variants?) is not confirmed by prior
-- investigation. Value is a plain legacy int (observed range in the hundreds to hundreds-of-thousands,
-- consistent with an in-game currency cost), not an FK to any other domain.
CREATE TABLE world.NpcGambleCosts
(
    NpcId      INT      NOT NULL,
    GambleTier SMALLINT NOT NULL,
    CostIndex  TINYINT  NOT NULL,
    Value      INT      NOT NULL,
    CONSTRAINT PK_NpcGambleCosts PRIMARY KEY CLUSTERED (NpcId, GambleTier, CostIndex),
    CONSTRAINT CK_NpcGambleCosts_GambleTier CHECK (GambleTier BETWEEN 0 AND 144),
    CONSTRAINT CK_NpcGambleCosts_CostIndex CHECK (CostIndex BETWEEN 0 AND 14),
    CONSTRAINT FK_NpcGambleCosts_Npcs FOREIGN KEY (NpcId) REFERENCES world.Npcs (NpcId)
);
