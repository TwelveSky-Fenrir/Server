-- Atomic conversion primitive between a character's wallet (game.Characters.Money) and its inventory
-- big-money pool (game.Characters.BigMoney) -- CZ_PROCESS_DATA_SEND tSort 246 (money->BigMoney) / 247
-- (BigMoney->money). Both columns already exist on the base schema, so this needs no migration.
--
-- This procedure is DELIBERATELY GENERIC (delta-in, guarded-bounds-out): it does not itself decide the
-- legacy "convert exactly one BigMoney unit" magnitude -- that resolution lives in
-- Fenrir.Application.Game.Domain.Inventory.BigMoneyUnitConversionPolicy (ResolveMoneyToBigMoney /
-- ResolveBigMoneyToMoney), the domain-side source of truth for tSort 246/247's exact behavior: 246 debits
-- the FULL requested amount from Money but credits exactly +1 BigMoney unit (a legacy value-loss quirk,
-- reproduced not "fixed" -- see that policy's own remarks) ; 247 debits exactly -1 BigMoney unit and credits
-- exactly +1,000,000,000 (MAX_NUMBER_SIZE1) Money regardless of how far above the eligibility floor the
-- requested count is. This procedure only re-enforces the two settled ceilings server-side (Money <=
-- 2,000,000,000 / MAX_NUMBER_SIZE, BigMoney <= 999 / MAX_NUMBER_SIZE2) against whatever deltas the caller
-- (already validated against BigMoneyUnitConversionPolicy) supplies, closing the same TOCTOU window every
-- other Adjust* money procedure in this schema closes -- same shape as usp_Character_AdjustMoney, whose own
-- header comment calls the missing BigMoney upper bound "a different, narrower quantity: the '1B' conversion
-- pathway", i.e. exactly this procedure.
--
-- Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:3512-3545 (ProcessForInventoryMoneyTo1BInventoryMoney, tSort
-- 246) ; :3547-3580 (ProcessFor1BInventoryMoneyToInventoryMoney, tSort 247) ; Server/Header/function.h:132-150
-- (CheckOverMaximum vs. CheckOverBigMoneyMaximum) ; Server/Header/Protocol/DEFINE.h:365-367 (MAX_NUMBER_SIZE =
-- 2,000,000,000 ; MAX_NUMBER_SIZE1 = 1,000,000,000 ; MAX_NUMBER_SIZE2 = 999).
--
-- economy-hardening pass (2026-07-12): this file existed and was already the sole live implementation behind
-- Fenrir.Application.Game.Services.Inventory.BigMoneyUnitConversionService (via
-- Fenrir.Data.Characters.BigMoneyRepository.AdjustBigMoneyConversionAsync, resolved through
-- Fenrir.Data.Abstractions.Characters.IBigMoneyRepository) but had never been appended to
-- Database/_manifest.txt -- so it had never actually been created in any database, and every call to
-- BigMoneyUnitConversionService.ConvertAsync (tSort 246/247) would have thrown "Could not find stored
-- procedure 'game.usp_Character_AdjustBigMoneyConversion'" the moment a player tried it. Fixed by appending it
-- to the manifest's C8 BigMoney group and registering error 50352 in
-- Migrations/Seed/admin/019_error_catalog_bigmoney_conversion.sql. No change to this procedure's own body was
-- needed -- it was already correctly written, just never deployed.
CREATE PROCEDURE game.usp_Character_AdjustBigMoneyConversion @CharacterId INT,
                                                             @DeltaMoney BIGINT,
                                                             @DeltaBigMoney INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    UPDATE game.Characters
    SET Money        = Money + @DeltaMoney,
        BigMoney     = BigMoney + @DeltaBigMoney,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId
      AND Money + @DeltaMoney BETWEEN 0 AND 2000000000
      AND BigMoney + @DeltaBigMoney BETWEEN 0 AND 999;

    IF
        @@ROWCOUNT = 0
        THROW 50352, N'Unknown character or insufficient/over-cap balance for this Money/BigMoney conversion adjustment.', 1;
END;
