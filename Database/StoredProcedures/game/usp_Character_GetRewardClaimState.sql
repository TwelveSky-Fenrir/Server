-- Same lazy weekly-reset computation as usp_Character_ClaimDailyReward, which is where the value is
-- physically rewritten. Uses DATEDIFF(DAY,0,x)/7, not DATEDIFF(WEEK,0,x): the latter follows the
-- session's DATEFIRST setting, not day zero's actual Monday.
CREATE PROCEDURE game.usp_Character_GetRewardClaimState @CharacterId INT, @TodayDate INT
AS
BEGIN
    SET
        NOCOUNT ON;

    DECLARE
        @TodayAsDate DATE = TRY_CONVERT(DATE, CAST(@TodayDate AS VARCHAR(8)), 112);

    -- CAST back to TINYINT: mixing a TINYINT branch with an INT literal would promote to INT, which the
    -- caller's TINYINT-typed field rejects.
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
