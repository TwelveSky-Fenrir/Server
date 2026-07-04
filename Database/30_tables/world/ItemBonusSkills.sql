-- Normalizes ITEM_INFO's iBonusSkillInfo[8][2]: one row per populated slot. pair[0]=SkillId (nullable, 0->NULL), pair[1]=Value (always present when the slot is populated).
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
