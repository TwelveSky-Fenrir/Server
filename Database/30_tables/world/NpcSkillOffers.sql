-- Normalizes both nSkillInfo1[3][8] and nSkillInfo2[3][3][3][8] into one sparse table (only nonzero slots kept); ArrayKind distinguishes the source array (1=SkillInfo1, 2=SkillInfo2).
-- ArrayKind=1: Tier=outer index (0-2), Dim2/Dim3 NULL, SlotIndex=inner 0-7. ArrayKind=2: nSkillInfo2 read as flat int[216], [a,b,c,d]->a*72+b*24+c*8+d; Tier/Dim2/Dim3=a/b/c, SlotIndex=d (meaning of the 3 extra dims beyond SkillInfo1 is unconfirmed).
-- Surrogate IDENTITY PK: the natural key (NpcId,ArrayKind,Tier,Dim2,Dim3,SlotIndex) can't be the PK since Dim2/Dim3 are nullable for ArrayKind=1.
CREATE TABLE world.NpcSkillOffers
(
    NpcSkillOfferId INT IDENTITY (1,1) NOT NULL,
    NpcId           INT     NOT NULL,
    ArrayKind       TINYINT NOT NULL,
    Tier            TINYINT NOT NULL,
    Dim2            TINYINT NULL,
    Dim3            TINYINT NULL,
    SlotIndex       TINYINT NOT NULL,
    SkillId         INT NULL,
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
