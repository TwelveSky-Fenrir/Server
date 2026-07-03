-- legacy mDropPotionInfo[5][2] -- normalized to one row per POPULATED slot (rate/id pair both non-zero;
-- a full-population scan of all 1139 monsters x 5 slots found zero cases of a partial pair, so "both
-- non-zero" and "either non-zero" give an identical row set here -- unlike MonsterDropExtraItems).
-- Pair order confirmed [rate, itemId] by cross-referencing decoded values against
-- Server/BuildEU33/005_00004DROP.CSV (e.g. monster 1 "Kobold": pairs (200000,2) and (20000,26) match
-- the CSV's field-for-field dump exactly) -- consistent with the DropExtraItemInfo convention.
-- PotionItemId is NOT NULL: every populated slot's item half is provably non-zero (see above), so no
-- 0->NULL translation is needed for this table (contrast MonsterDropExtraItems).
CREATE TABLE world.MonsterDropPotions
(
    MonsterId    INT     NOT NULL,
    SlotIndex    TINYINT NOT NULL, -- 0-4, position within mDropPotionInfo -- not a meaningful game id, just array position
    DropRate     INT     NOT NULL,
    PotionItemId INT     NOT NULL,
    CONSTRAINT PK_MonsterDropPotions PRIMARY KEY CLUSTERED (MonsterId, SlotIndex),
    CONSTRAINT FK_MonsterDropPotions_Monster FOREIGN KEY (MonsterId) REFERENCES world.Monsters (MonsterId),
    CONSTRAINT FK_MonsterDropPotions_Item FOREIGN KEY (PotionItemId) REFERENCES world.Items (ItemId),
    CONSTRAINT CK_MonsterDropPotions_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 4)
);
