-- Normalizes ITEM_INFO's iBonusSkillInfo[8][2]: one row per populated slot. pair[0]=SkillId (nullable, 0->NULL), pair[1]=Value (always present when the slot is populated).
-- No secondary index on SkillId: verified repo-wide (StoredProcedures/ + Views/) that the only reader of
-- this table (usp_Item_GetAll, the boot-cache load) scans it unfiltered -- nothing ever seeks this table
-- BY SkillId, so a reverse-lookup index here would only tax the one-time seed insert
-- (Migrations/Seed/world/081_item_bonus_skills.sql) for zero read benefit. Re-add if a "which items grant
-- skill X" GM-tooling query is ever built. (world.vw_ItemBonusSkillDetail, the one prior reader that joined
-- FROM this table INTO world.Skills, was deleted 2026-07-12 as a zero-consumer dead view.)
CREATE TABLE world.ItemBonusSkills
(
    ItemId    INT      NOT NULL,
    SlotIndex TINYINT  NOT NULL,
    SkillId   INT      NULL,
    Value     SMALLINT NOT NULL,
    CONSTRAINT PK_ItemBonusSkills PRIMARY KEY CLUSTERED (ItemId, SlotIndex),
    CONSTRAINT FK_ItemBonusSkills_Items FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId),
    CONSTRAINT FK_ItemBonusSkills_Skills FOREIGN KEY (SkillId) REFERENCES world.Skills (SkillId),
    CONSTRAINT CK_ItemBonusSkills_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 7)
);
