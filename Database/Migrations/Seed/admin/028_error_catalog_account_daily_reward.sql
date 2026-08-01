-- New file rather than an edit to an earlier error-catalog script: applied scripts are never edited.
-- Registers 50359 for game.usp_AccountDailyReward_Claim's account/character ownership check, introduced by
-- Migrations/005_account_daily_reward_key.sql when the daily-reward claim state moved from the character to
-- the account (legacy carries it on memberinfo, Server/BuildEU33/DB/nxtserver.sql:1153-1154, and persists it
-- WHERE uUserIdx, Server/ts25playuser/S08_MyDB.cpp:400).
-- Distinct from 50270 on purpose: 50270 is the ordinary "already claimed / cycle exhausted" refusal that the
-- claim path turns into a normal tResult=1 reply, whereas 50359 can only be reached by a session presenting a
-- character it does not own -- a protocol violation, not a gameplay outcome.
IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50359)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50359, 'game',
            N'usp_AccountDailyReward_Claim: the supplied character does not belong to the claiming account.');

-- 50270 is now thrown by game.usp_AccountDailyReward_Claim rather than the superseded
-- game.usp_Character_ClaimDailyReward, and its subject is the account, not the character. UPDATE, not a
-- guarded INSERT: the row already exists from Migrations/Seed/admin/004_error_catalog_v8_commerce.sql --
-- idempotent by construction, same convention as
-- Migrations/Seed/admin/016_error_catalog_offline_shop_withdraw_nothing.sql.
UPDATE admin.ErrorCatalog
SET Description = N'usp_AccountDailyReward_Claim: already claimed today, fully claimed, or unknown account.'
WHERE ErrorNumber = 50270;
