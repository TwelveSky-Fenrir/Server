CREATE OR ALTER PROCEDURE game.usp_Cash_Credit @AccountId INT,
                                               @Amount INT,
                                               @Reason TINYINT,
                                               @ProductId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @Amount < 1
        THROW 50241, N'Cash amount must be positive.', 1;

    BEGIN TRANSACTION;

    DECLARE @Credited TABLE
                      (
                          BalanceAfter INT
                      );

    UPDATE game.AccountCash
    SET Balance      = Balance + @Amount,
        UpdatedAtUtc = SYSUTCDATETIME()
    OUTPUT INSERTED.Balance
        INTO @Credited
    WHERE AccountId = @AccountId
      AND Balance + @Amount <= 2000000000;

    IF @@ROWCOUNT = 0
        BEGIN
            IF EXISTS (SELECT 1 FROM game.AccountCash WHERE AccountId = @AccountId)
                THROW 50360, N'Crediting this account''s cash balance would exceed the legacy cash cap (2,000,000,000).', 1;

            IF @Amount > 2000000000
                THROW 50360, N'Crediting this account''s cash balance would exceed the legacy cash cap (2,000,000,000).', 1;

            INSERT INTO game.AccountCash (AccountId, Balance)
            VALUES (@AccountId, @Amount);

            INSERT INTO @Credited (BalanceAfter)
            VALUES (@Amount);
        END;

    INSERT INTO game.CashLog (AccountId, Delta, BalanceAfter, Reason, ProductId)
    SELECT @AccountId, @Amount, BalanceAfter, @Reason, @ProductId
    FROM @Credited;

    COMMIT TRANSACTION;
END;
