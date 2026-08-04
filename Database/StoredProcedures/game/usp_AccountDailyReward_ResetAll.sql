CREATE OR ALTER PROCEDURE game.usp_AccountDailyReward_ResetAll @OccurrenceDate INT,
                                                             @ClearWeeklyDayCounter BIT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    DECLARE
        @OccurrenceDay DATE = TRY_CONVERT(DATE, CONVERT(CHAR(8), @OccurrenceDate), 112);

    IF @OccurrenceDay IS NULL
        THROW 50770, N'A valid daily reward reset occurrence date is required.', 1;

    IF @ClearWeeklyDayCounter IS NULL
        THROW 50781, N'A daily reward reset weekly flag is required.', 1;

    IF @ClearWeeklyDayCounter <> IIF(DATEDIFF(DAY, '19000101', @OccurrenceDay) % 7 = 0, 1, 0)
        THROW 50771, N'The daily reward reset weekly flag does not match its occurrence date.', 1;

    BEGIN TRANSACTION;

    IF NOT EXISTS (SELECT 1
                   FROM game.DailyRewardResetOccurrences WITH (UPDLOCK, HOLDLOCK)
                   WHERE OccurrenceDate = @OccurrenceDate)
        BEGIN
            UPDATE game.AccountDailyRewards
            SET RewardClaimDate = 0,
                RewardClaimDay  = CASE WHEN @ClearWeeklyDayCounter = 1 THEN 0 ELSE RewardClaimDay END,
                UpdatedAtUtc    = SYSUTCDATETIME()
            WHERE RewardClaimDate <> 0
               OR (@ClearWeeklyDayCounter = 1 AND RewardClaimDay <> 0);

            INSERT INTO game.DailyRewardResetOccurrences (OccurrenceDate)
            VALUES (@OccurrenceDate);
        END;

    COMMIT TRANSACTION;
END;
