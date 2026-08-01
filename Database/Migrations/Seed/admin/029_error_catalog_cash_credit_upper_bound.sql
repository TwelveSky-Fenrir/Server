-- New file rather than an edit to an earlier error-catalog script: applied scripts are never edited.
-- Registers 50360 for the new upper-bound guard added by usp_Cash_Credit_UpperBoundGuard.sql and
-- usp_Cash_CreditAndConsumeItem_UpperBoundGuard.sql (cash-balance-buy audit finding: legacy MemberInfo.uCash
-- is credited with no upper bound at all, Server/ts25extra/S08_MyDB.cpp:134 -- hardened here rather than
-- reproduced, same 2,000,000,000 cap convention as 50261/usp_Character_AdjustMoney). One shared number
-- across both procedures, matching how 50241 is already shared across all three usp_Cash_* procedures for
-- the identical "amount must be positive" precondition.
IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50360)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50360,
            'game',
            N'usp_Cash_Credit / usp_Cash_CreditAndConsumeItem: crediting this account''s cash balance would exceed the legacy cash cap (2,000,000,000).');
