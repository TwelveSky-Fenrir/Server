CREATE PROCEDURE game.usp_OfflineShop_WithdrawMoney @CharacterId INT,
                                                    @ExpectedMoney INT,
                                                    @ExpectedBigMoney INT,
                                                    @TodayDate INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @ExpectedMoney = 0 AND @ExpectedBigMoney = 0
        THROW 50340, N'Nothing to withdraw from this offline shop.', 1;

    BEGIN TRANSACTION;

    UPDATE game.OfflineShops
    SET Money    = 0,
        BigMoney = 0
    WHERE CharacterId = @CharacterId
      AND ShopState = 0
      AND ShopDate >= @TodayDate
      AND Money = @ExpectedMoney
      AND BigMoney = @ExpectedBigMoney;

    IF @@ROWCOUNT = 0
        THROW 50276, N'Offline shop is not closed, has expired, or its earnings no longer match the expected amounts.', 1;

    UPDATE game.Characters
    SET Money        = Money + @ExpectedMoney,
        BigMoney     = BigMoney + @ExpectedBigMoney,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId
      AND Money + @ExpectedMoney BETWEEN 0 AND 2000000000
      AND BigMoney + @ExpectedBigMoney BETWEEN 0 AND 999;

    IF @@ROWCOUNT = 0
        BEGIN
            IF EXISTS (SELECT 1
                       FROM game.Characters
                       WHERE CharacterId = @CharacterId
                         AND BigMoney + @ExpectedBigMoney > 999)
                THROW 50333, N'Crediting this offline shop''s BigMoney earnings would exceed the legacy BigMoney cap (MAX_NUMBER_SIZE2 = 999).', 1;

            THROW 50261, N'Withdrawal would exceed the legacy money cap (MAX_NUMBER_SIZE = 2,000,000,000).', 1;
        END;

    COMMIT TRANSACTION;
END;
