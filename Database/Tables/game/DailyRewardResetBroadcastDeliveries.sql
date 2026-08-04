CREATE TABLE game.DailyRewardResetBroadcastDeliveries
(
    OccurrenceDate             INT              NOT NULL,
    ShardId                    TINYINT          NOT NULL,
    BroadcastLeaseId           UNIQUEIDENTIFIER NULL,
    BroadcastLeaseExpiresAtUtc DATETIME2(3)     NULL,
    BroadcastCompletedAtUtc    DATETIME2(3)     NULL,
    CONSTRAINT PK_DailyRewardResetBroadcastDeliveries PRIMARY KEY CLUSTERED (OccurrenceDate, ShardId),
    CONSTRAINT FK_DailyRewardResetBroadcastDeliveries_Occurrence FOREIGN KEY (OccurrenceDate)
        REFERENCES game.DailyRewardResetOccurrences (OccurrenceDate),
    CONSTRAINT CK_DailyRewardResetBroadcastDeliveries_ShardId CHECK (ShardId > 0),
    CONSTRAINT CK_DailyRewardResetBroadcastDeliveries_LeaseState CHECK
        ((BroadcastCompletedAtUtc IS NULL
            AND ((BroadcastLeaseId IS NULL AND BroadcastLeaseExpiresAtUtc IS NULL)
                OR (BroadcastLeaseId IS NOT NULL AND BroadcastLeaseExpiresAtUtc IS NOT NULL)))
            OR (BroadcastCompletedAtUtc IS NOT NULL
                AND BroadcastLeaseId IS NULL
                AND BroadcastLeaseExpiresAtUtc IS NULL))
);
