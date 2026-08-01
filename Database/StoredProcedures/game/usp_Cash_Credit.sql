-- First credit for an account mints its AccountCash row; a race of two first-credits is
-- backstopped by PK_AccountCash (the losing insert's transaction retries at the caller).
CREATE PROCEDURE game.usp_Cash_Credit @AccountId INT,
                                      @Amount INT,
                                      @Reason TINYINT,
                                      @ProductId INT = NULL
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    IF
        @Amount < 1
        THROW 50241, N'Cash amount must be positive.', 1;

    DECLARE
        @Credited TABLE
                  (
                      BalanceAfter INT
                  );

    BEGIN
        TRANSACTION;

    UPDATE game.AccountCash
    SET Balance      = Balance + @Amount,
        UpdatedAtUtc = SYSUTCDATETIME()
    OUTPUT INSERTED.Balance
        INTO @Credited
    WHERE AccountId = @AccountId;

    IF
        @@ROWCOUNT = 0
        BEGIN
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
