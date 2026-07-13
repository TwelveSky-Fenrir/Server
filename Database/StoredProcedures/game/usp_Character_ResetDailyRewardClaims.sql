-- CenterServer world-state authority: the daily (and Monday-weekly) reward-claim reset feeding
-- ICenterDailyRewardReset / DailyResetHost (Fenrir.Cluster.WorldState).
--
-- CORRECTS legacy Bug 4: the legacy daily reset wrote to a hard-coded MemberInfo table name
-- (Server/ts25center/S07_MyGame01.cpp:230,233), bypassing the configurable table-slot indirection every other
-- statement in the subsystem uses. Routing it through this proc keeps a repointed member table consistent with
-- the rest of the system.
--
-- The daily reward-claim state and the weekly day counter live on game.Characters (the same two columns the
-- lazy per-character path uses -- usp_Character_ClaimDailyReward / usp_Character_GetRewardClaimState):
--   * RewardClaimDate (INT, YYYYMMDD of last claim; 0 = unclaimed) IS the "claimed today" marker -- a claim is
--     available again once RewardClaimDate <> today. Always reset to 0 here so every member can claim again.
--   * RewardClaimDay  (TINYINT 0-7, weekly per-day counter) is additionally zeroed only on the Monday variant,
--     when @ClearWeeklyDayCounter = 1.
--
-- One guarded UPDATE, so no explicit transaction (a single statement is already atomic); the WHERE only touches
-- rows that actually need changing, keeping this once-a-day bulk write off every already-reset row of the
-- busiest table in the schema. Not touching a member is not an error -- this proc has no THROW / error code.
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
