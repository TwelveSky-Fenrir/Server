-- database/50_procedures/game/usp_Character_GetRewardClaimState.sql
-- Contract: @CharacterId -> RS0 { RewardClaimDay, RewardClaimDate }, exactly one row (0 rows if the
-- character does not exist -- caller treats that as "unknown character", same posture as every other
-- by-CharacterId read in this schema).
-- Backs CZ_GET_REWARD_ITEM_SEND (ZC_GET_REWARD_ITEM_RECV.RewardDay).
-- Read-only, safe to retry.
-- CORRECTION (review finding): the legacy's mPLAYUSER.uRewardClaimDay resets to 0 every MONDAY via a
-- scheduled ts25center daily tick (Server/ts25center/S07_MyGame01.cpp:218-238, broadcast tSort=1600,
-- applied zone-side at Server/ts25zone/S07_MyGame08.cpp:1322-1348) -- a prior pass here only ever
-- INCREMENTED RewardClaimDay (capped 0-7 by CK_Characters_RewardClaimDay), so the 7-day cycle never
-- actually recurred; it was a one-time reward for the character's entire lifetime. Rather than adding a
-- scheduled background job (a real operational surface this schema doesn't otherwise need), the WEEKLY
-- reset is computed LAZILY here from the ALREADY-STORED RewardClaimDate: DATEDIFF(DAY, 0, x) / 7 (integer
-- division) buckets any date into its Monday-to-Sunday week -- 1900-01-01 (SQL Server's day zero) is
-- itself a Monday, a fixed calendar fact, so every 7-day span starting there lands on the same integer.
-- Deliberately NOT DATEDIFF(WEEK, 0, x): that intrinsic's week boundary follows the session's DATEFIRST
-- setting (default 7/Sunday-start in en-US), NOT day zero's actual weekday -- silently bucketing
-- Sunday-Saturday instead of Monday-Sunday and splitting a real Mon-Sun cycle across two "weeks" a session
-- with a different DATEFIRST would only make worse. If @TodayDate falls in a different DAY/7 bucket than
-- the last recorded claim, the EFFECTIVE day-counter this read reports is 0, exactly as if the scheduled
-- legacy reset had already run. The stored value itself is only physically rewritten the next time
-- usp_Character_ClaimDailyReward runs (same lazy idiom), which is observably identical to an eager
-- scheduled reset from every caller's point of view.
-- Params:
--   @CharacterId INT
--   @TodayDate   INT -- caller-supplied YYYYMMDD (app clock), same convention as
--                        usp_Character_ClaimDailyReward's own @TodayDate
CREATE PROCEDURE game.usp_Character_GetRewardClaimState @CharacterId INT, @TodayDate INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TodayAsDate DATE = TRY_CONVERT(DATE, CAST(@TodayDate AS VARCHAR(8)), 112);

    -- CAST back to TINYINT explicitly -- a CASE mixing a TINYINT branch (RewardClaimDay) with an INT
    -- literal (0) promotes the whole expression to INT, which the caller's TINYINT-typed DTO field
    -- rejects at read time.
    SELECT RewardClaimDay = CAST(CASE
                                      WHEN RewardClaimDate <> 0
                                           AND DATEDIFF(DAY, 0, @TodayAsDate) / 7 <>
                                               DATEDIFF(DAY, 0, TRY_CONVERT(DATE, CAST(RewardClaimDate AS VARCHAR(8)), 112)) / 7
                                          THEN 0
                                      ELSE RewardClaimDay
                                      END AS TINYINT),
           RewardClaimDate
    FROM game.Characters
    WHERE CharacterId = @CharacterId;
END;
