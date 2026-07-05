-- Legacy mDropPotionInfo[5][2]; one row per populated slot (rate/id pairs are always both-zero or both-non-zero here, unlike MonsterDropExtraItems).
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
