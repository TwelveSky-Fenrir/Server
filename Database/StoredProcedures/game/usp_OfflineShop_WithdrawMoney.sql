-- database/50_procedures/game/usp_OfflineShop_WithdrawMoney.sql
-- CAS guard: requires the shop closed and @ExpectedMoney/@ExpectedBigMoney to still match (anti-race
-- double-check). The guarded UPDATE against game.Characters bounds BOTH Money (existing) and BigMoney
-- (upper bound 999, MAX_NUMBER_SIZE2 -- Server/Header/Protocol/DEFINE.h:367): legacy rejects a withdrawal
-- outright when crediting the shop's accumulated BigMoney would push the character's own BigMoney past 999
-- (Server/ts25zone/S07_MyGame09.cpp:906-912) -- without this bound, repeated list/sell/withdraw cycles could
-- push a character's BigMoney arbitrarily above 999 even though each shop's own accumulation is separately
-- capped there by usp_OfflineShop_ExecutePurchase.
CREATE PROCEDURE game.usp_OfflineShop_WithdrawMoney @CharacterId INT,
                                                     @ExpectedMoney INT,
                                                     @ExpectedBigMoney INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @ExpectedMoney = 0 AND @ExpectedBigMoney = 0
        THROW 50276, N'Nothing to withdraw from this offline shop.', 1;

    BEGIN TRANSACTION;

    UPDATE game.OfflineShops
    SET Money    = 0,
        BigMoney = 0
    WHERE CharacterId = @CharacterId
      AND ShopState = 0
      AND Money = @ExpectedMoney
      AND BigMoney = @ExpectedBigMoney;

    IF @@ROWCOUNT = 0
        THROW 50276, N'Offline shop is not closed, or its earnings no longer match the expected amounts.', 1;

    UPDATE game.Characters
    SET Money        = Money + @ExpectedMoney,
        BigMoney     = BigMoney + @ExpectedBigMoney,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId
      AND Money + @ExpectedMoney BETWEEN 0 AND 2000000000
      AND BigMoney + @ExpectedBigMoney BETWEEN 0 AND 999;

    IF @@ROWCOUNT = 0
        BEGIN
            -- Diagnostic re-read only; picks which error code to throw.
            IF EXISTS (SELECT 1
                       FROM game.Characters
                       WHERE CharacterId = @CharacterId
                         AND BigMoney + @ExpectedBigMoney > 999)
                THROW 50333, N'Crediting this offline shop''s BigMoney earnings would exceed the legacy BigMoney cap (MAX_NUMBER_SIZE2 = 999).', 1;

            THROW 50261, N'Withdrawal would exceed the legacy money cap (MAX_NUMBER_SIZE = 2,000,000,000).', 1;
        END;

    COMMIT TRANSACTION;
END;
