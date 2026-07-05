-- Rejects (does not clamp) an adjustment that would drive Money or Money2 negative.
-- A missing AccountVault row (never initialized) is treated as insufficient funds.
CREATE PROCEDURE game.usp_AccountVault_AdjustMoney @AccountId   INT,
    @DeltaMoney  BIGINT,
    @DeltaMoney2 BIGINT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

UPDATE game.AccountVault
SET Money        = Money + @DeltaMoney,
    Money2       = Money2 + @DeltaMoney2,
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE AccountId = @AccountId
  AND Money + @DeltaMoney >= 0
  AND Money2 + @DeltaMoney2 >= 0;

IF
@@ROWCOUNT = 0
        THROW 50221, N'Insufficient account vault balance for this adjustment.', 1;
END;
