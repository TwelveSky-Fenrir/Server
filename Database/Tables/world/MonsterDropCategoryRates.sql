-- Legacy mDropItemInfo[12], an undecoded pass-through: slots 0-2/3-5/6-8/9-11 are believed to be COMMON/UNIQUE/RARE/ELITE rate tiers (data_translate_monster.h) but the actual rate formula isn't reverse-engineered.
CREATE TABLE world.MonsterDropCategoryRates
(
    MonsterId     INT     NOT NULL,
    CategoryIndex TINYINT NOT NULL, -- 0-11, raw mDropItemInfo array position (see comment for the COMMON/UNIQUE/RARE/ELITE slot-grouping hint)
    Value         INT     NOT NULL,
    CONSTRAINT PK_MonsterDropCategoryRates PRIMARY KEY CLUSTERED (MonsterId, CategoryIndex),
    CONSTRAINT FK_MonsterDropCategoryRates_Monster FOREIGN KEY (MonsterId) REFERENCES world.Monsters (MonsterId),
    CONSTRAINT CK_MonsterDropCategoryRates_CategoryIndex CHECK (CategoryIndex BETWEEN 0 AND 11),
    -- Value-range bound mirrors legacy's load-time field validation (Monster_CheckValidElement,
    -- Server/Header/S15_MyShare.cpp:1801-1808).
    CONSTRAINT CK_MonsterDropCategoryRates_Value CHECK (Value BETWEEN 0 AND 1000000)
);
