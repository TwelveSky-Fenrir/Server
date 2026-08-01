-- economy-hardening pass (2026-07-12): registers the single BigMoney-conversion error code that was, until
-- this pass, thrown by a stored procedure that existed on disk but had never been appended to
-- Database/_manifest.txt -- usp_Character_AdjustBigMoneyConversion (tSort 246/247, Money<->BigMoney unit
-- conversion) is the live implementation behind
-- Fenrir.Application.Game.Services.Inventory.BigMoneyUnitConversionService, so this was a genuine
-- would-crash-on-first-call gap, not a dead-code cleanup. See usp_Character_AdjustBigMoneyConversion.sql's own
-- header for the full citation.
--
-- Supersedes (does not reuse) the identical row that used to live in the now-deleted, never-manifested
-- 018_error_catalog_bigmoney_transfers.sql -- that file also carried stale 50350/50351 rows describing a
-- procedure name (usp_Character_AdjustBigMoneyStore) that no longer exists; 50350/50351 are correctly owned
-- by the pet-bag procedures (Migrations/044_character_petbag_procs.sql) and are NOT re-registered here.
IF NOT EXISTS (SELECT 1
               FROM admin.ErrorCatalog
               WHERE ErrorNumber = 50352)
    INSERT INTO admin.ErrorCatalog (ErrorNumber, SchemaName, Description)
    VALUES (50352, 'game',
            N'usp_Character_AdjustBigMoneyConversion: unknown character or insufficient/over-cap balance for this Money/BigMoney conversion adjustment (tSort 246/247).');
