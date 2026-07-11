-- Legacy LEVEL_INFO; Level is the real legacy level number (not IDENTITY), the PK other domains FK onto.
-- RangeInfo3 (RangeInfo[2]) is not part of the ExpRangeMin/Max EXP band; its exact game-mechanic purpose is unconfirmed, so it keeps a neutral name.
-- RangeInfo3 mirrors the legacy 0-100 bound (Server/Header/S15_MyShare.cpp:843-846, Level_CheckValidElement);
-- TINYINT alone permits 0-255, wider than the legacy rule, hence the explicit CHECK. Complements, not
-- replaces, Application/Fenrir.Application.Game.GameData/WorldDataCacheBuilder.cs's ValidateLevels boot-time
-- check (same contract, same bound) -- that C# check only runs once per GameServer boot against whatever is
-- in the table at that moment, so it cannot by itself stop an out-of-range value from ever being written by
-- a future bad reseed/hand-edit; this constraint closes that gap at write time. The other four gaps in the
-- same contract (ExpRangeMin/ExpRangeMax's 2,000,000,000 upper bound, the strict ExpRangeMin < ExpRangeMax
-- requirement, the inter-row exact-tiling requirement, and the Life/Mana 0-10000 bound) are either cross-row
-- (tiling, not expressible as a single-row CHECK) or already narrower than their own column type without
-- needing one for the currently-seeded data, so they stay C#-only validation per that method's own remarks.
CREATE TABLE world.Levels
(
    Level         SMALLINT NOT NULL,
    ExpRangeMin   INT      NOT NULL,
    ExpRangeMax   INT      NOT NULL,
    RangeInfo3    TINYINT  NOT NULL,
    AttackPower   SMALLINT NOT NULL,
    DefensePower  SMALLINT NOT NULL,
    AttackSuccess SMALLINT NOT NULL,
    AttackBlock   SMALLINT NOT NULL,
    ElementAttack SMALLINT NOT NULL,
    Life          INT      NOT NULL,
    Mana          INT      NOT NULL,
    CONSTRAINT PK_Levels PRIMARY KEY CLUSTERED (Level),
    CONSTRAINT CK_Levels_RangeInfo3 CHECK (RangeInfo3 BETWEEN 0 AND 100)
);
