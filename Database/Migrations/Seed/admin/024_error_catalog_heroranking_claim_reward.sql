-- Registers 50357/50358 for the new usp_HeroRanking_ClaimReward (transaction-composition audit, 2026-07-12):
-- HeroRewardClaimService previously durably set RewardClaimed=1 via usp_HeroRanking_Upsert and only granted
-- the reward through an in-memory zone-command mirror, so a crash or a full inbox between the two could burn
-- the claim forever without the reward ever landing durably. See usp_HeroRanking_ClaimReward.sql's own header
-- for the full writeup. (Numbered 024/50357-50358, not 023/50355-50356: a concurrent same-session pass
-- already claimed 023/50355 for usp_Character_ApplyTribeFourConversion's quota-consumption branch.)
IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50357)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50357, 'game',
            N'usp_HeroRanking_ClaimReward: reward already claimed, or no claimable ranking row for this character/period.');

IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50358)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50358, 'game', N'usp_HeroRanking_ClaimReward: unknown character for the contribution-points grant.');
