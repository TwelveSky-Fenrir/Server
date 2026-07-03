-- legacy mDropMoneyInfo[3] -- always present (not sparse: every monster has these 3 slots, even if
-- all-zero for the 644/1139 monsters that never drop money, e.g. static "EXP Tower"/"stone" objects),
-- so this is a 1:1 extension table, one row per monster, not a populated-slot-only child table like
-- the potion/extra-item drops below.
-- Column order/naming follows what real data actually shows, not the task brief's suggested naming:
-- field[0] is near-constant (129600 for ordinary monsters, 1000000 for a handful, 0 when no money
-- drops at all) so it reads as a rate numerator/threshold rather than an amount; fields[1]/[2] always
-- satisfy field[1] <= field[2] across all 1139 monsters and scale with monster level, so they are
-- confidently Min/Max amount. The exact roll formula that consumes DropRate is not reverse-engineered
-- (ServerDocs/12_ts25zone/14_MyGame05.md only documents the pipeline stage name, "DROP_MONEY, argent,
-- plafonne a MAX_NUMBER_SIZE") -- kept as an honest pass-through value, not a guessed percentage.
CREATE TABLE world.MonsterDropMoney
(
    MonsterId INT NOT NULL,
    DropRate  INT NOT NULL,
    MinAmount INT NOT NULL,
    MaxAmount INT NOT NULL,
    CONSTRAINT PK_MonsterDropMoney PRIMARY KEY CLUSTERED (MonsterId),
    CONSTRAINT FK_MonsterDropMoney_Monster FOREIGN KEY (MonsterId) REFERENCES world.Monsters (MonsterId),
    CONSTRAINT CK_MonsterDropMoney_AmountRange CHECK (MinAmount <= MaxAmount)
);
