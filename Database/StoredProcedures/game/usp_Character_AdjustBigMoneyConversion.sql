CREATE PROCEDURE game.usp_Character_AdjustBigMoneyConversion @CharacterId INT,
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
      AND BigMoney + @DeltaBigMoney BETWEEN 0 AND 999;

    IF
        @@ROWCOUNT = 0
        THROW 50352, N'Unknown character or insufficient/over-cap balance for this Money/BigMoney conversion adjustment.', 1;
END;
