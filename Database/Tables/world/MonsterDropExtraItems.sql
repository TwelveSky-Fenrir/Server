CREATE TABLE world.MonsterDropExtraItems
(
    MonsterId INT     NOT NULL,
    SlotIndex TINYINT NOT NULL,
    DropRate  INT     NOT NULL,
    ItemId    INT     NULL,
    CONSTRAINT PK_MonsterDropExtraItems PRIMARY KEY CLUSTERED (MonsterId, SlotIndex),
    CONSTRAINT FK_MonsterDropExtraItems_Monster FOREIGN KEY (MonsterId) REFERENCES world.Monsters (MonsterId),
    CONSTRAINT FK_MonsterDropExtraItems_Item FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId),
    CONSTRAINT CK_MonsterDropExtraItems_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 49),
    CONSTRAINT CK_MonsterDropExtraItems_DropRate CHECK (DropRate BETWEEN 0 AND 1000000),
    CONSTRAINT CK_MonsterDropExtraItems_ItemId CHECK (ItemId IS NULL OR ItemId BETWEEN 0 AND 99999)
);
