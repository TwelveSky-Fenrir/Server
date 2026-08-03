CREATE TABLE world.MonsterDropQuestItems
(
    MonsterId   INT NOT NULL,
    DropRate    INT NOT NULL,
    QuestItemId INT NOT NULL,
    CONSTRAINT PK_MonsterDropQuestItems PRIMARY KEY CLUSTERED (MonsterId),
    CONSTRAINT FK_MonsterDropQuestItems_Monster FOREIGN KEY (MonsterId) REFERENCES world.Monsters (MonsterId),
    CONSTRAINT FK_MonsterDropQuestItems_Item FOREIGN KEY (QuestItemId) REFERENCES world.Items (ItemId),
    CONSTRAINT CK_MonsterDropQuestItems_DropRate CHECK (DropRate BETWEEN 0 AND 1000000),    
    CONSTRAINT CK_MonsterDropQuestItems_QuestItemId CHECK (QuestItemId BETWEEN 0 AND 99999) 
);
