-- Normalizes BOTH nSkillInfo1[3][8] and nSkillInfo2[3][3][3][8] (Header/Protocol/STRUCT.h:213-234) into
-- one sparse table -- only nonzero slots kept. Both arrays are overwhelmingly zero, as expected for a
-- population that is mostly vendors/quest-givers, not skill trainers: only 9 of the 131 real NPCs use
-- SkillInfo1 at all (117 rows), and only 1 (NpcId 51) uses SkillInfo2 (117 rows) -- 234 rows total against
-- a 31440-slot ceiling (131 NPCs * (24 + 216)).
-- ArrayKind distinguishes the source array (1 = nSkillInfo1, 2 = nSkillInfo2) so both can share one shape:
--   * ArrayKind 1: Tier is nSkillInfo1's outer index (0-2, observed to mean "which weapon-tier skill
--     tutor track" -- the 9 real users are named "...Tutor.../Master.../Swordmaster..." etc. in matched
--     Tier/SlotIndex groups of 3), Dim2/Dim3 are always NULL, SlotIndex is the inner 0-7 slot.
--   * ArrayKind 2: nSkillInfo2 is read by NpcReader as a flat int[216] standing in for the legacy
--     [3][3][3][8] declaration, with [a,b,c,d] -> a*72 + b*24 + c*8 + d (72 = 3*3*8, 24 = 3*8) -- see
--     NpcRecord's XML doc. Tier/Dim2/Dim3 map to a/b/c respectively, SlotIndex to d. The real-world
--     meaning of having 3 extra outer dimensions beyond SkillInfo1's single Tier is NOT confirmed by prior
--     investigation (NpcId 51's populated cells resemble several near-duplicate copies of a SkillInfo1-
--     shaped 24-cell block, suggesting a per-branch/per-tribe repeat of the same tier structure, but that
--     is an observation, not a confirmed decode) -- both arrays are therefore stored dimension-for-
--     dimension exactly as the legacy layout defines them, rather than collapsed or reinterpreted.
-- Surrogate NpcSkillOfferId IDENTITY PK: SQL Server PRIMARY KEY columns must be NOT NULL, but Dim2/Dim3
-- are legitimately NULL for every ArrayKind=1 row, so the natural key can only be enforced via a nullable
-- UNIQUE constraint, not the PK itself.
-- SkillId is NULL, never 0, per the shared cross-domain FK convention -- not actually exercised by this
-- generator (a zero slot simply isn't a row), kept nullable/FK'd defensively like the other tables here.
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
