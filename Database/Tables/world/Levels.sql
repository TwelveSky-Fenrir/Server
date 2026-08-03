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
