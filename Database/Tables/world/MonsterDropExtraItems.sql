-- Legacy mDropExtraItemInfo[50][2], the per-monster loot table; one row per populated slot (rate/id pair not both zero), including partial pairs (rate-only or item-only) that real data actually has.
CREATE TABLE world.MonsterDropExtraItems
(
    MonsterId INT     NOT NULL,
    SlotIndex TINYINT NOT NULL, -- 0-49, position within mDropExtraItemInfo -- not a meaningful game id, just array position
    DropRate  INT     NOT NULL,
    ItemId    INT     NULL,     -- NULL when the legacy slot's item half was 0 (rate set, no item wired up)
    CONSTRAINT PK_MonsterDropExtraItems PRIMARY KEY CLUSTERED (MonsterId, SlotIndex),
    CONSTRAINT FK_MonsterDropExtraItems_Monster FOREIGN KEY (MonsterId) REFERENCES world.Monsters (MonsterId),
    CONSTRAINT FK_MonsterDropExtraItems_Item FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId),
    CONSTRAINT CK_MonsterDropExtraItems_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 49),
    INDEX IX_MonsterDropExtraItems_ItemId NONCLUSTERED (ItemId)
);
