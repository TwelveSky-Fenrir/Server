CREATE OR ALTER PROCEDURE game.usp_AccountDailyReward_GetState @AccountId INT, @TodayDate INT
AS
BEGIN
    SET
        NOCOUNT ON;

    IF
        NOT EXISTS (SELECT 1
                    FROM game.Characters
                    WHERE AccountId = @AccountId)
        RETURN;

    DECLARE
        @TodayAsDate DATE = TRY_CONVERT(DATE, CAST(@TodayDate AS VARCHAR(8)), 112);
    DECLARE
        @Day TINYINT = 0;
    DECLARE
        @Date INT = 0;

    SELECT @Day = RewardClaimDay,
           @Date = RewardClaimDate
    FROM game.AccountDailyRewards
    WHERE AccountId = @AccountId;

    SELECT RewardClaimDay  = CAST(CASE
                                      WHEN @Date <> 0
                                          AND DATEDIFF(DAY, 0, @TodayAsDate) / 7 <>
                                              DATEDIFF(DAY, 0, TRY_CONVERT(DATE, CAST(@Date AS VARCHAR(8)), 112)) / 7
                                          THEN 0
                                      ELSE @Day
        END AS TINYINT),
           RewardClaimDate = @Date;
END;
