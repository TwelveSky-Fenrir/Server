-- procs-game-economy-inventory audit (basse severite): usp_Cash_Debit was removed as dead code in the
-- 2026-07-12 Database/ cleanup pass (its own job folded into usp_Cash_DebitAndGrantItem), but 50240/50241's
-- catalog descriptions still named the deleted procedure as their sole thrower. Both codes are still THROWn
-- today, just by different/additional procedures -- 50240 by usp_Cash_DebitAndGrantItem alone, and 50241 by
-- usp_Cash_DebitAndGrantItem, usp_Cash_Credit, AND the newer usp_Cash_CreditAndConsumeItem. UPDATE, not a
-- guarded INSERT, since both rows already exist from Migrations/Seed/admin/002_error_catalog_a3.sql --
-- idempotent by construction (re-running this statement is a no-op once applied), same convention as
-- Migrations/Seed/admin/016_error_catalog_offline_shop_withdraw_nothing.sql's own 50276 refresh.
UPDATE admin.ErrorCatalog
SET Description = N'usp_Cash_DebitAndGrantItem: insufficient cash balance for this debit.'
WHERE ErrorNumber = 50240;

UPDATE admin.ErrorCatalog
SET Description = N'usp_Cash_DebitAndGrantItem/usp_Cash_Credit/usp_Cash_CreditAndConsumeItem: cash amount must be positive.'
WHERE ErrorNumber = 50241;
