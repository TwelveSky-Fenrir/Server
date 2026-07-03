-- Normalizes ITEM_INFO's iBonusSkillInfo[8][2] (Header/Protocol/STRUCT.h:44-93): one row per populated
-- slot (pair[0] != 0 OR pair[1] != 0), not 16 mostly-empty columns -- only 15,417 of 34,353 real items
-- use any of the 8 slots at all, and among those, 1-2 populated slots is by far the most common case.
-- Pair-order interpretation was verified against real data, not assumed from the field name alone:
-- every non-zero pair[0] value is a valid world.Skills SkillId; pair[1] is never 0 when pair[0] is
-- non-zero (0 of 20,763 such slots); and the reverse -- pair[0] == 0 but pair[1] != 0 -- does occur
-- (1,407 slots, e.g. weapon items with only a bare "value" and no granted skill). That confirms
-- pair[0] = SkillId (nullable, legacy "0 = no skill" sentinel translated to NULL) and pair[1] = Value
-- (always present whenever the slot is populated at all, so it stays NOT NULL).
CREATE TABLE world.ItemBonusSkills
(
    ItemId    INT      NOT NULL,
    SlotIndex TINYINT  NOT NULL,
    SkillId   INT NULL,
    Value     SMALLINT NOT NULL,
    CONSTRAINT PK_ItemBonusSkills PRIMARY KEY CLUSTERED (ItemId, SlotIndex),
    CONSTRAINT FK_ItemBonusSkills_Items FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId),
    CONSTRAINT FK_ItemBonusSkills_Skills FOREIGN KEY (SkillId) REFERENCES world.Skills (SkillId),
    CONSTRAINT CK_ItemBonusSkills_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 7)
);
