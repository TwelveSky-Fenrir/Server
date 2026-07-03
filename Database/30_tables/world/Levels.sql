-- Source: legacy LEVEL_INFO (005_00001.IMG via LevelReader.ReadAll -- no runtime patch exists for this
-- file, ReadAll is byte-identical to ReadAllRaw). One row per LevelRecord, all 145 -- the Index set was
-- verified to be exactly {1..145} with no gaps or duplicates, so this is a genuine leaf reference table
-- other domains' tables can safely FK onto (see PK contract: Level SMALLINT, not IDENTITY, the real
-- legacy level number).
-- ExpRangeMin/ExpRangeMax are RangeInfo[0]/[1]: verified as a perfectly contiguous EXP band across all
-- 145 rows (every level's ExpRangeMin == the previous level's ExpRangeMax + 1), i.e. the EXP thresholds
-- that bound this character level. RangeInfo[2] (here RangeInfo3) is NOT part of that band -- it only
-- ranges 0-50 while the other two run into the billions -- so it keeps its neutral positional name: its
-- exact game-mechanic purpose (it climbs roughly every 5-19 levels to 50 at level 113, then drops to 0
-- for 114-145) isn't independently confirmed, and guessing a semantic name here would misrepresent it.
-- AttackPower/AttackSuccess/AttackBlock/ElementAttack are always 0 in the real data (only DefensePower,
-- Life, and Mana actually vary by level) but are kept as columns for 1:1 fidelity with LEVEL_INFO
-- (Header/Protocol/STRUCT.h:16-27) -- a future data revision could populate them.
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
    CONSTRAINT PK_Levels PRIMARY KEY CLUSTERED (Level)
);
