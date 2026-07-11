-- Atomic transfer between a character's on-hand BigMoney (game.Characters.BigMoney) and its own Store/coffre
-- BigMoney pool (game.Characters.BigStoreMoney) -- CZ_PROCESS_DATA_SEND tSort 241 (Inventory->Store,
-- DeltaBigMoney negative/DeltaBigStoreMoney positive) / 244 (Store->Inventory, the reverse). Both columns
-- already exist on game.Characters (Tables/game/Characters.sql) since the initial schema, so this is a plain
-- StoredProcedures/ script -- unlike the pet-bag/Save-BigMoney additions in this same wave, no new column is
-- introduced here. Same "single guarded UPDATE, no explicit transaction needed" shape as
-- usp_Character_AdjustStoreMoney.
-- Réf. C++ : Server/ts25zone/S04_MyWork05.cpp:3666-3699 (ProcessForInventoryMoneyTo1BStoreMoney) ; :3701-3734
-- (ProcessFor1BStoreMoneyToInventoryMoney) ; Server/Header/Protocol/DEFINE.h:367 (MAX_NUMBER_SIZE2 = 999, the
-- BigMoney-family cap both pools share).
CREATE PROCEDURE game.usp_Character_AdjustBigStoreMoney @CharacterId        INT,
                                                         @DeltaBigMoney      INT,
                                                         @DeltaBigStoreMoney INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- Guarded UPDATE closes a TOCTOU: two concurrent transfers must never jointly breach either 999 cap or
    -- drive either pool negative.
    UPDATE game.Characters
    SET BigMoney      = BigMoney + @DeltaBigMoney,
        BigStoreMoney = BigStoreMoney + @DeltaBigStoreMoney,
        UpdatedAtUtc  = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId
      AND BigMoney + @DeltaBigMoney BETWEEN 0 AND 999
      AND BigStoreMoney + @DeltaBigStoreMoney BETWEEN 0 AND 999;

    IF @@ROWCOUNT = 0
        THROW 50349, N'Unknown character or insufficient balance for this BigMoney/BigStoreMoney adjustment.', 1;
END;
