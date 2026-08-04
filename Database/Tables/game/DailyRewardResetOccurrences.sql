CREATE TABLE game.DailyRewardResetOccurrences
(
    OccurrenceDate      INT          NOT NULL,
    ResetCompletedAtUtc DATETIME2(3) NOT NULL
        CONSTRAINT DF_DailyRewardResetOccurrences_ResetCompletedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_DailyRewardResetOccurrences PRIMARY KEY CLUSTERED (OccurrenceDate),
    CONSTRAINT CK_DailyRewardResetOccurrences_OccurrenceDate CHECK (OccurrenceDate BETWEEN 19000101 AND 99991231)
);
