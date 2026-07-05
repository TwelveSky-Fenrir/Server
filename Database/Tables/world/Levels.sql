-- Legacy LEVEL_INFO; Level is the real legacy level number (not IDENTITY), the PK other domains FK onto.
-- RangeInfo3 (RangeInfo[2]) is not part of the ExpRangeMin/Max EXP band; its exact game-mechanic purpose is unconfirmed, so it keeps a neutral name.
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
