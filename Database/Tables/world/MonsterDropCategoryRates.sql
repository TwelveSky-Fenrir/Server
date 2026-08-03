CREATE TABLE world.MonsterDropCategoryRates
(
    MonsterId     INT     NOT NULL,
    CategoryIndex TINYINT NOT NULL, 
    Value         INT     NOT NULL,
    CONSTRAINT PK_MonsterDropCategoryRates PRIMARY KEY CLUSTERED (MonsterId, CategoryIndex),
    CONSTRAINT FK_MonsterDropCategoryRates_Monster FOREIGN KEY (MonsterId) REFERENCES world.Monsters (MonsterId),
    CONSTRAINT CK_MonsterDropCategoryRates_CategoryIndex CHECK (CategoryIndex BETWEEN 0 AND 11),
    CONSTRAINT CK_MonsterDropCategoryRates_Value CHECK (Value BETWEEN 0 AND 1000000)
);
