CREATE TABLE world.MonsterDropPotions
(
    MonsterId    INT     NOT NULL,
    SlotIndex    TINYINT NOT NULL,                                                         
    DropRate     INT     NOT NULL,
    PotionItemId INT     NOT NULL,
    CONSTRAINT PK_MonsterDropPotions PRIMARY KEY CLUSTERED (MonsterId, SlotIndex),
    CONSTRAINT FK_MonsterDropPotions_Monster FOREIGN KEY (MonsterId) REFERENCES world.Monsters (MonsterId),
    CONSTRAINT FK_MonsterDropPotions_Item FOREIGN KEY (PotionItemId) REFERENCES world.Items (ItemId),
    CONSTRAINT CK_MonsterDropPotions_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 4),
    CONSTRAINT CK_MonsterDropPotions_DropRate CHECK (DropRate BETWEEN 0 AND 1000000),      
    CONSTRAINT CK_MonsterDropPotions_PotionItemId CHECK (PotionItemId BETWEEN 0 AND 99999) 
);
