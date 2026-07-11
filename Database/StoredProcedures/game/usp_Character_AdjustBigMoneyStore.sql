-- Atomic 1:1 unit transfer between a character's inventory big-money pool (game.Characters.BigMoney) and
-- its own Store big-money pool (game.Characters.BigStoreMoney) -- CZ_PROCESS_DATA_SEND tSort 241
-- (inventory->store) / 244 (store->inventory). Both columns already live on the same game.Characters row
-- (BigMoney/BigStoreMoney are both part of the base schema), so a single guarded UPDATE is already atomic --
-- no explicit transaction needed, matching usp_Character_AdjustStoreMoney's own shape (this procedure's
-- regular-money sibling). The only difference from that sibling is the 999-unit ceiling (MAX_NUMBER_SIZE2)
-- enforced on BOTH sides here, instead of the regular 2,000,000,000 (MAX_NUMBER_SIZE) cap -- BigMoney has no
-- such cap in usp_Character_AdjustMoney itself; that procedure's own header comment calls the missing bound
-- "a different, narrower quantity: the '1B' conversion pathway", i.e. exactly this procedure and its two
-- siblings (usp_AccountVault_TransferBigMoneyWithCharacter, usp_Character_AdjustBigMoneyConversion).
--
-- Domain-side resolved deltas: Fenrir.Application.Game.Domain.Inventory.BigMoneyTransferPolicy
-- (ResolveInventoryToStore/ResolveStoreToInventory) already computes and validates the symmetric
-- +-requestedQuantity delta pair before this procedure is ever called; this procedure's own guarded UPDATE
-- independently re-validates the same two bounds server-side so a stale/racing caller can never push either
-- pool out of range regardless of what was already checked in memory.
--
-- Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:3666-3699 (ProcessForInventoryMoneyTo1BStoreMoney, tSort 241) ;
-- :3701-3734 (ProcessFor1BStoreMoneyToInventoryMoney, tSort 244) ; Server/Header/Protocol/DEFINE.h:365-367
-- (MAX_NUMBER_SIZE2 = 999, the BigMoney-family cap).
CREATE PROCEDURE game.usp_Character_AdjustBigMoneyStore @CharacterId INT,
                                                        @DeltaBigMoney INT,
                                                        @DeltaBigStoreMoney INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    UPDATE game.Characters
    SET BigMoney      = BigMoney + @DeltaBigMoney,
        BigStoreMoney = BigStoreMoney + @DeltaBigStoreMoney,
        UpdatedAtUtc  = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId
      AND BigMoney + @DeltaBigMoney BETWEEN 0 AND 999
      AND BigStoreMoney + @DeltaBigStoreMoney BETWEEN 0 AND 999;

    IF
        @@ROWCOUNT = 0
        THROW 50349, N'Unknown character or insufficient/over-cap balance for this BigMoney/BigStoreMoney adjustment (999-unit MAX_NUMBER_SIZE2 cap).', 1;
END;
