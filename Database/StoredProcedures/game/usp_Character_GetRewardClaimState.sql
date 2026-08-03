CREATE PROCEDURE game.usp_Character_GetRewardClaimState @CharacterId INT, @TodayDate INT
AS
BEGIN
    SET
        NOCOUNT ON;

    DECLARE
        @TodayAsDate DATE = TRY_CONVERT(DATE, CAST(@TodayDate AS VARCHAR(8)), 112);

    SELECT RewardClaimDay = CAST(CASE
                                     WHEN RewardClaimDate <> 0
                                         AND DATEDIFF(DAY, 0, @TodayAsDate) / 7 <>
                                             DATEDIFF(DAY, 0,
                                                      TRY_CONVERT(DATE, CAST(RewardClaimDate AS VARCHAR(8)), 112)) /
                                             7
                                         THEN 0
                                     ELSE RewardClaimDay
        END AS TINYINT),
           RewardClaimDate
    FROM game.Characters
    WHERE CharacterId = @CharacterId;
END;
