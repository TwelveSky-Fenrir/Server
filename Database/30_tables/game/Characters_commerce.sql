-- Additive V8 (Player Commerce & Cash) migration for game.Characters. Separate script instead of editing
-- Characters_progression.sql -- the migrator journal (admin.SchemaVersions) forbids changing an applied
-- script's content -- additive ALTER is the sanctioned corrective path (_manifest.txt header), same
-- convention Characters_progression.sql itself documents.
-- Legacy mapping:
--   BloodCoin       <- wAvatar.aBloodCoin (CZ_BUY_BLOOD_MARK_SEND spend currency, contracts/04_commerce.md
--                      "USE_BLOOD" family, S04_MyWork02.cpp:15321). No modeled EARN path in this batch
--                      (the legacy source available to this investigation never shows a write site for
--                      aBloodCoin, only reads/decrements at spend time) -- starts at 0 for every character,
--                      an explicit open issue (see V8 StructuredOutput), not a guess.
--   RewardClaimDay  <- mPLAYUSER's uRewardClaimDay (CZ_GET/CLAIM_REWARD_ITEM_SEND, 7-day login-reward
--                      cycle): 0..6 = next claimable day, 7 = fully claimed. CORRECTED (review finding):
--                      the legacy resets this to 0 every MONDAY via a scheduled ts25center daily tick
--                      (Server/ts25center/S07_MyGame01.cpp:218-238, broadcast tSort=1600, applied zone-side
--                      at Server/ts25zone/S07_MyGame08.cpp:1322-1348) -- this IS a real, recurring weekly
--                      cycle in the legacy, not a one-time lifetime reward. Fenrir computes this reset
--                      LAZILY from RewardClaimDate below (see usp_Character_GetRewardClaimState/
--                      usp_Character_ClaimDailyReward's own header comments) rather than adding a scheduled
--                      background job -- observably identical from every caller's point of view.
--   RewardClaimDate <- models the legacy's opaque per-session uRewardClaimState ("already claimed today")
--                      as an explicit YYYYMMDD int (same encoding/idiom as OfflineShops.ShopDate) instead:
--                      "claimed today" == RewardClaimDate equals today's date. uRewardClaimState itself
--                      resets to 0 every day (same ts25center tick cited above) -- comparing against
--                      today's date reproduces that exact CLIENT-observable contract (one claim per
--                      calendar day) without needing the scheduled job. ALSO now the reference point the
--                      weekly RewardClaimDay reset is computed from (DATEDIFF(DAY, 0, x) / 7 bucketing,
--                      NOT the DATEFIRST-dependent DATEDIFF(WEEK, 0, x) -- see usp_Character_
--                      GetRewardClaimState's own header). 0 = never claimed (a real date is always > 0).
ALTER TABLE game.Characters
    ADD BloodCoin INT NOT NULL CONSTRAINT DF_Characters_BloodCoin DEFAULT 0,
        RewardClaimDay  TINYINT NOT NULL CONSTRAINT DF_Characters_RewardClaimDay DEFAULT 0,
        RewardClaimDate INT     NOT NULL CONSTRAINT DF_Characters_RewardClaimDate DEFAULT 0;
GO

ALTER TABLE game.Characters
    ADD CONSTRAINT CK_Characters_BloodCoin CHECK (BloodCoin >= 0),
        CONSTRAINT CK_Characters_RewardClaimDay CHECK (RewardClaimDay BETWEEN 0 AND 7);
GO
