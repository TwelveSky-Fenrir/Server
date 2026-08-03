CREATE TABLE world.NpcSkillOffers
(
    NpcSkillOfferId INT IDENTITY (1,1) NOT NULL,
    NpcId           INT                NOT NULL,
    ArrayKind       TINYINT            NOT NULL,
    Tier            TINYINT            NOT NULL,
    Dim2            TINYINT            NULL,
    Dim3            TINYINT            NULL,
    SlotIndex       TINYINT            NOT NULL,
    SkillId         INT                NULL,
    CONSTRAINT PK_NpcSkillOffers PRIMARY KEY CLUSTERED (NpcSkillOfferId),
    CONSTRAINT UQ_NpcSkillOffers_Slot UNIQUE (NpcId, ArrayKind, Tier, Dim2, Dim3, SlotIndex),
    CONSTRAINT CK_NpcSkillOffers_ArrayKind CHECK (ArrayKind IN (1, 2)),
    CONSTRAINT CK_NpcSkillOffers_Tier CHECK (Tier BETWEEN 0 AND 2),
    CONSTRAINT CK_NpcSkillOffers_Dim2 CHECK (Dim2 IS NULL OR Dim2 BETWEEN 0 AND 2),
    CONSTRAINT CK_NpcSkillOffers_Dim3 CHECK (Dim3 IS NULL OR Dim3 BETWEEN 0 AND 2),
    CONSTRAINT CK_NpcSkillOffers_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 7),
    CONSTRAINT FK_NpcSkillOffers_Npcs FOREIGN KEY (NpcId) REFERENCES world.Npcs (NpcId),
    CONSTRAINT FK_NpcSkillOffers_Skills FOREIGN KEY (SkillId) REFERENCES world.Skills (SkillId)
);
