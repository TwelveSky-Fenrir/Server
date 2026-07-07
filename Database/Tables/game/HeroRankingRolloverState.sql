-- Singleton sentinel for the weekly Current->Previous rollover (game.usp_HeroRanking_Rollover). The legacy
-- keeps its sentinel as a fake uCharIdx=0 row baked directly into herorankpre (S08_MyDB.cpp:358-404); that
-- trick doesn't fit here since game.HeroRankings.CharacterId is FK'd to a real game.Characters row, so the
-- date lives in its own singleton table instead (same shape as game.WorldState/game.TowerState: Id CHECK'd
-- to 1, row created lazily by the proc that owns it on its own first-ever call).
CREATE TABLE game.HeroRankingRolloverState
(
    Id                TINYINT      NOT NULL
        CONSTRAINT DF_HeroRankingRolloverState_Id DEFAULT 1,
    LastRolloverAtUtc DATETIME2(3) NOT NULL
        CONSTRAINT DF_HeroRankingRolloverState_LastRolloverAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_HeroRankingRolloverState PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT CK_HeroRankingRolloverState_Id CHECK (Id = 1)
);
