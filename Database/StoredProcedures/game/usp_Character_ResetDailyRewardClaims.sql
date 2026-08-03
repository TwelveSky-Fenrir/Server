CREATE PROCEDURE game.usp_Character_ResetDailyRewardClaims @ClearWeeklyDayCounter BIT
AS
BEGIN
    SET
        NOCOUNT ON;

    UPDATE game.Characters
    SET RewardClaimDate = 0,
        RewardClaimDay  = CASE WHEN @ClearWeeklyDayCounter = 1 THEN 0 ELSE RewardClaimDay END,
        UpdatedAtUtc    = SYSUTCDATETIME()
    WHERE RewardClaimDate <> 0
       OR (@ClearWeeklyDayCounter = 1 AND RewardClaimDay <> 0);
END;
