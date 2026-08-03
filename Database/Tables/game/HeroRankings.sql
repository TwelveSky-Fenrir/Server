CREATE TABLE game.HeroRankings
(
    CharacterId   INT           NOT NULL,
    PeriodKind    TINYINT       NOT NULL,
    Points        INT           NOT NULL
        CONSTRAINT DF_HeroRankings_Points DEFAULT 0,
    TribeId       TINYINT       NULL,
    Level         SMALLINT      NULL, 
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
