-- Lower (>=0) and upper (MAX_NUMBER_SIZE=2,000,000,000) bounds are folded into one guarded UPDATE, not a
-- separate pre-check, to close a TOCTOU where two concurrent credits could jointly breach the cap.
-- BigMoney has no upper cap enforced here -- legacy's cap on it guards a different, narrower quantity
-- (the "1B" conversion pathway); documented gap, not an oversight.
CREATE PROCEDURE game.usp_Character_AdjustMoney @CharacterId INT,
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
      AND BigMoney + @DeltaBigMoney >= 0;

    IF
        @@ROWCOUNT = 0
        BEGIN
            -- Diagnostic re-read only (no TOCTOU risk): picks which of the two error codes to throw.
            IF
                EXISTS (SELECT 1
                        FROM game.Characters
                        WHERE CharacterId = @CharacterId
                          AND Money + @DeltaMoney > 2000000000)
                THROW 50261, N'Adjustment would exceed the legacy money cap (MAX_NUMBER_SIZE = 2,000,000,000).', 1;

            THROW
                50222, N'Unknown character or insufficient money balance for this adjustment.', 1;
        END;
END;
