CREATE TABLE game.TowerState
(
    TowerIndex         TINYINT      NOT NULL,
    Level              TINYINT      NOT NULL
        CONSTRAINT DF_TowerState_Level DEFAULT (0),
    TowerType          TINYINT      NOT NULL
        CONSTRAINT DF_TowerState_TowerType DEFAULT (0),
    AttackState        SMALLINT     NOT NULL
        CONSTRAINT DF_TowerState_AttackState DEFAULT (-1),
    ControllingTribeId TINYINT      NULL,
    CapturedAtUtc      DATETIME2(3) NULL,
    CONSTRAINT PK_TowerState PRIMARY KEY CLUSTERED (TowerIndex),
    CONSTRAINT CK_TowerState_TowerIndex CHECK (TowerIndex BETWEEN 0 AND 11),
    CONSTRAINT CK_TowerState_Level CHECK (Level IN (0, 1, 2, 4, 6, 8, 9)),
    CONSTRAINT CK_TowerState_TowerType CHECK (TowerType BETWEEN 0 AND 3),
    CONSTRAINT CK_TowerState_AttackState CHECK (AttackState IN (-1, 0)),
    CONSTRAINT FK_TowerState_Tribe FOREIGN KEY (ControllingTribeId) REFERENCES game.Tribes (TribeId)
);
