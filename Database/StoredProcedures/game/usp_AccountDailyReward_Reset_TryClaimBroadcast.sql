CREATE OR ALTER PROCEDURE game.usp_AccountDailyReward_Reset_TryClaimBroadcast @OccurrenceDate INT,
                                                                                @ShardId TINYINT,
                                                                                @LeaseId UNIQUEIDENTIFIER,
                                                                                @LeaseDurationSeconds INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    IF TRY_CONVERT(DATE, CONVERT(CHAR(8), @OccurrenceDate), 112) IS NULL
        THROW 50777, N'A valid daily reward reset occurrence date is required.', 1;

    IF @ShardId IS NULL OR @ShardId = 0
        THROW 50779, N'A daily reward reset broadcast shard is required.', 1;

    IF @LeaseId IS NULL OR @LeaseId = '00000000-0000-0000-0000-000000000000'
        THROW 50772, N'A non-empty daily reward reset broadcast lease is required.', 1;

    IF @LeaseDurationSeconds IS NULL OR @LeaseDurationSeconds NOT BETWEEN 1 AND 300
        THROW 50773, N'The daily reward reset broadcast lease duration is invalid.', 1;

    DECLARE
        @ClaimState TINYINT = 2,
        @BroadcastCompletedAtUtc DATETIME2(3),
        @BroadcastLeaseExpiresAtUtc DATETIME2(3),
        @NowUtc DATETIME2(3) = SYSUTCDATETIME();

    BEGIN TRANSACTION;

    IF NOT EXISTS (SELECT 1
                   FROM game.DailyRewardResetOccurrences WITH (UPDLOCK, HOLDLOCK)
                   WHERE OccurrenceDate = @OccurrenceDate)
        THROW 50774, N'The daily reward reset occurrence has not been durably applied.', 1;

    SELECT @BroadcastCompletedAtUtc = BroadcastCompletedAtUtc,
           @BroadcastLeaseExpiresAtUtc = BroadcastLeaseExpiresAtUtc
    FROM game.DailyRewardResetBroadcastDeliveries WITH (UPDLOCK, HOLDLOCK)
    WHERE OccurrenceDate = @OccurrenceDate
      AND ShardId = @ShardId;

    IF @@ROWCOUNT = 0
        BEGIN
            INSERT INTO game.DailyRewardResetBroadcastDeliveries
                (OccurrenceDate, ShardId, BroadcastLeaseId, BroadcastLeaseExpiresAtUtc)
            VALUES (@OccurrenceDate, @ShardId, @LeaseId, DATEADD(SECOND, @LeaseDurationSeconds, @NowUtc));

            SET @ClaimState = 0;
        END
    ELSE IF @BroadcastCompletedAtUtc IS NOT NULL
        SET @ClaimState = 1;
    ELSE IF @BroadcastLeaseExpiresAtUtc IS NULL OR @BroadcastLeaseExpiresAtUtc <= @NowUtc
        BEGIN
            UPDATE game.DailyRewardResetBroadcastDeliveries
            SET BroadcastLeaseId = @LeaseId,
                BroadcastLeaseExpiresAtUtc = DATEADD(SECOND, @LeaseDurationSeconds, @NowUtc)
            WHERE OccurrenceDate = @OccurrenceDate
              AND ShardId = @ShardId;

            SET @ClaimState = 0;
        END;

    COMMIT TRANSACTION;

    SELECT ClaimState = @ClaimState;
END;
