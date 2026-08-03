CREATE PROCEDURE game.usp_TribeBank_DepositFromCharacter @TribeId TINYINT,
                                                         @SlotIndex TINYINT,
                                                         @CharacterId INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    DECLARE
        @Debited TABLE
                 (
                     Amount INT
                 );

    BEGIN
        TRANSACTION;

    UPDATE game.Characters
    SET Money        = 0,
        UpdatedAtUtc = SYSUTCDATETIME()
    OUTPUT DELETED.Money
        INTO @Debited (Amount)
    WHERE CharacterId = @CharacterId
      AND Money >= 1;

    IF
        @@ROWCOUNT = 0
        THROW 50212, N'Character has no money to deposit into the tribe bank.', 1;

    DECLARE
        @Amount INT = (SELECT Amount FROM @Debited);

    EXEC game.usp_TribeBank_Deposit @TribeId = @TribeId, @SlotIndex = @SlotIndex, @Amount = @Amount;

    DECLARE
        @NewSlotAmount INT;
    SELECT @NewSlotAmount = Amount
    FROM game.TribeBank WITH (SNAPSHOT)
    WHERE TribeId = @TribeId
      AND SlotIndex = @SlotIndex;

    INSERT INTO game.TribeBankLog (TribeId, SlotIndex, CharacterId, Delta, BalanceAfter)
    VALUES (@TribeId, @SlotIndex, @CharacterId, @Amount, @NewSlotAmount);

    COMMIT TRANSACTION;

    SELECT Money
    FROM game.Characters
    WHERE CharacterId = @CharacterId;
END;
