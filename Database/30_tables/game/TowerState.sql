-- Normalizes legacy `towerinfo` (nxtserver.sql, a singleton row wIndex=1 with Tower0..Tower11 columns) into
-- one row per tower -- the "12 fixed columns on a singleton row" shape is exactly the anti-pattern this
-- migration is asking to avoid. Direct inspection of Protocol/STRUCT.h's TOWER_INFO shows the real runtime
-- struct actually tracks TWO parallel 12-element state arrays (mState1Tower/mState2Tower) -- but towerinfo
-- only ever persists ONE value per tower slot (Tower0..11) to MySQL, so the second in-memory state array is
-- transient/runtime-only and was never a persistence concern; nothing is lost by not modeling it here.
-- CapturedAtUtc is new (no legacy MySQL column) -- a real audit timestamp for "when did this tower last
-- change hands", useful for capture-cooldown/scoring logic the flat legacy int alone can't answer.
-- PLAIN (not memory-optimized) table: needs an enforced FOREIGN KEY to game.Tribes (a regular disk-based
-- table -- see game.HeroRankings for the same In-Memory OLTP constraint), and tower captures are rare
-- events, not a per-tick hot path.
-- Starts empty (no existing players yet) -- schema only, no seed. The 12 rows (TowerIndex 0-11) are created
-- by game.usp_TowerState_EnsureInitialized, an idempotent bootstrap the application calls once at world
-- startup, since GameServer can only ever reach this schema through a procedure (no direct table access).
CREATE TABLE game.TowerState
(
    TowerIndex         TINYINT NOT NULL,
    ControllingTribeId TINYINT NULL,
    CapturedAtUtc      DATETIME2(3) NULL,
    CONSTRAINT PK_TowerState PRIMARY KEY CLUSTERED (TowerIndex),
    CONSTRAINT CK_TowerState_TowerIndex CHECK (TowerIndex BETWEEN 0 AND 11),
    CONSTRAINT FK_TowerState_Tribe FOREIGN KEY (ControllingTribeId) REFERENCES game.Tribes (TribeId)
);
