CREATE OR ALTER PROCEDURE game.usp_AccountDailyReward_Reset_CompleteBroadcast @OccurrenceDate INT,
                                                                              @ShardId TINYINT,
                                                                              @LeaseId UNIQUEIDENTIFIER
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    IF TRY_CONVERT(DATE, CONVERT(CHAR(8), @OccurrenceDate), 112) IS NULL
        THROW 50778, N'A valid daily reward reset occurrence date is required.', 1;

    IF @ShardId IS NULL OR @ShardId = 0
        THROW 50780, N'A daily reward reset broadcast shard is required.', 1;

    IF @LeaseId IS NULL OR @LeaseId = '00000000-0000-0000-0000-000000000000'
        THROW 50775, N'A non-empty daily reward reset broadcast lease is required.', 1;

    DECLARE
        @Completed BIT = 0,
        @BroadcastCompletedAtUtc DATETIME2(3);

    BEGIN TRANSACTION;

    SELECT @BroadcastCompletedAtUtc = BroadcastCompletedAtUtc
    FROM game.DailyRewardResetBroadcastDeliveries
    WITH (UPDLOCK, HOLDLOCK)
    WHERE OccurrenceDate = @OccurrenceDate
      AND ShardId = @ShardId;

    IF @@ROWCOUNT = 0
        THROW 50776, N'The daily reward reset broadcast delivery has not been claimed.', 1;

    IF @BroadcastCompletedAtUtc IS NOT NULL
        SET @Completed = 1;
    ELSE
        IF EXISTS (SELECT 1
                   FROM game.DailyRewardResetBroadcastDeliveries
                   WHERE OccurrenceDate = @OccurrenceDate
                     AND ShardId = @ShardId
                     AND BroadcastLeaseId = @LeaseId)
            BEGIN
                UPDATE game.DailyRewardResetBroadcastDeliveries
                SET BroadcastLeaseId           = NULL,
                    BroadcastLeaseExpiresAtUtc = NULL,
                    BroadcastCompletedAtUtc    = SYSUTCDATETIME()
                WHERE OccurrenceDate = @OccurrenceDate
                  AND ShardId = @ShardId
                  AND BroadcastLeaseId = @LeaseId;

                SET @Completed = 1;
            END;

    COMMIT TRANSACTION;

    SELECT Completed = @Completed;
END;
