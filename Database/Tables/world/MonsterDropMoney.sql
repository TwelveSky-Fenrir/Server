CREATE TABLE world.MonsterDropMoney
(
    MonsterId INT NOT NULL,
    DropRate  INT NOT NULL,
    MinAmount INT NOT NULL,
    MaxAmount INT NOT NULL,
    CONSTRAINT PK_MonsterDropMoney PRIMARY KEY CLUSTERED (MonsterId),
    CONSTRAINT FK_MonsterDropMoney_Monster FOREIGN KEY (MonsterId) REFERENCES world.Monsters (MonsterId),
    CONSTRAINT CK_MonsterDropMoney_AmountRange CHECK (MinAmount <= MaxAmount),
    CONSTRAINT CK_MonsterDropMoney_DropRate CHECK (DropRate BETWEEN 0 AND 1000000),     
    CONSTRAINT CK_MonsterDropMoney_MinAmount CHECK (MinAmount BETWEEN 0 AND 100000000), 
    CONSTRAINT CK_MonsterDropMoney_MaxAmount CHECK (MaxAmount BETWEEN 0 AND 100000000)  
);
