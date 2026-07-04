-- Normalizes legacy `towerinfo` (a singleton row with Tower0..Tower11 columns) into one row per tower.
-- The 12 rows (TowerIndex 0-11) are created by game.usp_TowerState_EnsureInitialized, an idempotent
-- bootstrap the application calls once at world startup -- callers must not INSERT directly.
CREATE TABLE game.TowerState
(
    TowerIndex         TINYINT NOT NULL,
    ControllingTribeId TINYINT NULL,
    CapturedAtUtc      DATETIME2(3) NULL,
    CONSTRAINT PK_TowerState PRIMARY KEY CLUSTERED (TowerIndex),
    CONSTRAINT CK_TowerState_TowerIndex CHECK (TowerIndex BETWEEN 0 AND 11),
    CONSTRAINT FK_TowerState_Tribe FOREIGN KEY (ControllingTribeId) REFERENCES game.Tribes (TribeId)
);
