-- legacy mDropItemInfo[12] -- honest, undecoded pass-through. Prior investigation could NOT fully
-- resolve what these 12 rate/category slots mean: Server/ts25ztool/data_translate_monster.h names
-- slots 0-2/3-5/6-8/9-11 COMMON0-2/UNIQUE0-2/RARE0-2/ELITE0-2 (kept here only as a slot-index hint in
-- this comment, NOT modeled as a category-name column -- we do not know the exact rate formula), and
-- ServerDocs/12_ts25zone/14_MyGame05.md confirms these feed "DROP_GENERAL_ITEM" resolved at runtime via
-- mITEM.ReturnDropRareItem -- an item-table lookup we have not reverse-engineered. Values observed up
-- to 1000000 (not a 0-100 percentage), and ELITE0-2 (slots 9-11) appear to be dead/commented code in at
-- least one legacy build variant, so guessing a formula here would risk baking in a wrong interpretation.
-- Only the 2130 non-zero slots (of 13668 possible = 1139 x 12) are seeded; average 1.87 populated
-- slots/monster.
CREATE TABLE world.MonsterDropCategoryRates
(
    MonsterId     INT     NOT NULL,
    CategoryIndex TINYINT NOT NULL, -- 0-11, raw mDropItemInfo array position (see comment for the COMMON/UNIQUE/RARE/ELITE slot-grouping hint)
    Value         INT     NOT NULL,
    CONSTRAINT PK_MonsterDropCategoryRates PRIMARY KEY CLUSTERED (MonsterId, CategoryIndex),
    CONSTRAINT FK_MonsterDropCategoryRates_Monster FOREIGN KEY (MonsterId) REFERENCES world.Monsters (MonsterId),
    CONSTRAINT CK_MonsterDropCategoryRates_CategoryIndex CHECK (CategoryIndex BETWEEN 0 AND 11)
);
