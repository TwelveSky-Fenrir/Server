CREATE TABLE game.HeroRankingRolloverState
(
    Id                TINYINT      NOT NULL
        CONSTRAINT DF_HeroRankingRolloverState_Id DEFAULT 1,
    LastRolloverAtUtc DATETIME2(3) NOT NULL
        CONSTRAINT DF_HeroRankingRolloverState_LastRolloverAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_HeroRankingRolloverState PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT CK_HeroRankingRolloverState_Id CHECK (Id = 1)
);
