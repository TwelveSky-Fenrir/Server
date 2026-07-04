-- database/50_procedures/game/usp_OfflineShop_WithdrawMoney.sql
-- Contract: atomically withdraw accumulated offline-shop sale proceeds into the owner's own live money --
-- CZ_SET_DEPUTY_PSHOP_MONEY_SEND (contracts/04_commerce.md, verified S07_MyGame09.cpp:886-957: requires
-- the shop closed (ShopState=0), the caller's expected amounts to still match [anti-race double-check],
-- and not both zero). D7 regime (b).
-- Params: @CharacterId INT, @ExpectedMoney INT, @ExpectedBigMoney INT.
-- Result set: none.
-- Idempotent: no.
-- Errors:
--   THROW 50276 -- both expected amounts are zero (nothing to withdraw), the shop is not closed, or its
--                  earnings no longer match the expected amounts.
--   THROW 50261 -- crediting the character's own money would exceed the legacy money cap (shared with
--                  usp_Character_AdjustMoney).
CREATE PROCEDURE game.usp_OfflineShop_WithdrawMoney
    @CharacterId      INT,
    @ExpectedMoney    INT,
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
      AND BigMoney + @ExpectedBigMoney >= 0;

    IF @@ROWCOUNT = 0
        THROW 50261, N'Withdrawal would exceed the legacy money cap (MAX_NUMBER_SIZE = 2,000,000,000).', 1;

    COMMIT TRANSACTION;
END;
