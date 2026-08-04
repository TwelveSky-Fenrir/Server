CREATE PROCEDURE game.usp_TribeBank_Withdraw @TribeId TINYINT,
                                             @SlotIndex TINYINT,
                                             @CharacterId INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    DECLARE
        @Amount INT;

    BEGIN
        TRANSACTION;

    SELECT @Amount = Amount
    FROM game.TribeBank WITH (SNAPSHOT)
    WHERE TribeId = @TribeId
      AND SlotIndex = @SlotIndex;

    IF
        @Amount IS NULL OR @Amount < 1
        THROW 50210, N'Insufficient tribe bank balance for this withdrawal.', 1;

    UPDATE game.TribeBank
        WITH (SNAPSHOT)
    SET Amount = 0
    WHERE TribeId = @TribeId
      AND SlotIndex = @SlotIndex
      AND Amount = @Amount;

    IF
        @@ROWCOUNT = 0
        THROW 50210, N'Insufficient tribe bank balance for this withdrawal.', 1;

    UPDATE game.Characters
    SET Money        = Money + @Amount,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId
      AND Money + @Amount BETWEEN 0 AND 2000000000;

    IF
        @@ROWCOUNT = 0
        THROW 50261, N'Adjustment would exceed the legacy money cap (MAX_NUMBER_SIZE = 2,000,000,000).', 1;

    INSERT INTO game.TribeBankLog (TribeId, SlotIndex, CharacterId, Delta, BalanceAfter)
    VALUES (@TribeId, @SlotIndex, @CharacterId, -@Amount, 0);

    COMMIT TRANSACTION;

    SELECT Money
    FROM game.Characters
    WHERE CharacterId = @CharacterId;
END;
