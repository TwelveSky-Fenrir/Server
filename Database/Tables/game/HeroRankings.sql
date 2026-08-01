-- Merges legacy `herorankcur`/`herorankpre` into one table with a period marker: PeriodKind 0 = Current,
-- 1 = Previous. herorankcur has no hDate/hAccept/hDesc of its own, so RewardClaimed/Description are only
-- meaningful once a period rolls to Previous -- both NULLable rather than given a fake default.
CREATE TABLE game.HeroRankings
(
    CharacterId   INT           NOT NULL,
    PeriodKind    TINYINT       NOT NULL,
    Points        INT           NOT NULL
        CONSTRAINT DF_HeroRankings_Points DEFAULT 0,
    TribeId       TINYINT       NULL,
    Level         SMALLINT      NULL, -- sourced from game.Characters.Level (SMALLINT); widened to INT here was an unjustified type drift, aligned to match
    RewardClaimed BIT           NULL,
    Description   NVARCHAR(255) NULL,
    RecordedAtUtc DATETIME2(3)  NOT NULL
        CONSTRAINT DF_HeroRankings_RecordedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_HeroRankings PRIMARY KEY CLUSTERED (CharacterId, PeriodKind),
    CONSTRAINT CK_HeroRankings_PeriodKind CHECK (PeriodKind IN (0, 1)),
    CONSTRAINT FK_HeroRankings_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId),
    CONSTRAINT FK_HeroRankings_Tribe FOREIGN KEY (TribeId) REFERENCES game.Tribes (TribeId),
    INDEX IX_HeroRankings_Period_Points NONCLUSTERED (PeriodKind, Points DESC)
);
