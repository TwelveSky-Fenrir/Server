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
