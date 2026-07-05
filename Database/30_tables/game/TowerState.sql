-- Normalizes legacy `towerinfo` (a singleton row with Tower0..Tower11 columns) into one row per tower.
-- The 12 rows (TowerIndex 0-11) are created by game.usp_TowerState_EnsureInitialized, an idempotent
-- bootstrap the application calls once at world startup -- callers must not INSERT directly.
-- Level/TowerType mirror the legacy mTowerInfo->mState1Tower[i] packed broadcast value (decoded level*100+type,
-- MyGame::GetTowerState/GetTowerType) so a GameServer restart resumes the guardian-monster lifecycle instead of
-- losing it back to Application.Fenrir.Game.Progression.TowerWarState's in-memory-only defaults.
CREATE TABLE game.TowerState
(
    TowerIndex         TINYINT NOT NULL,
    Level              TINYINT NOT NULL CONSTRAINT DF_TowerState_Level DEFAULT (0),
    TowerType          TINYINT NOT NULL CONSTRAINT DF_TowerState_TowerType DEFAULT (0),
    ControllingTribeId TINYINT NULL,
    CapturedAtUtc      DATETIME2(3) NULL,
    CONSTRAINT PK_TowerState PRIMARY KEY CLUSTERED (TowerIndex),
    CONSTRAINT CK_TowerState_TowerIndex CHECK (TowerIndex BETWEEN 0 AND 11),
    CONSTRAINT CK_TowerState_Level CHECK (Level IN (0, 2, 4, 6, 8)),
    CONSTRAINT CK_TowerState_TowerType CHECK (TowerType BETWEEN 0 AND 3),
    CONSTRAINT FK_TowerState_Tribe FOREIGN KEY (ControllingTribeId) REFERENCES game.Tribes (TribeId)
);
