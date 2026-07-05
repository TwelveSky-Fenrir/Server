-- Idempotent bootstrap: creates an all-zero AccountVault row on first call; a no-op thereafter.
CREATE PROCEDURE game.usp_AccountVault_EnsureInitialized @AccountId INT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

    IF
NOT EXISTS (SELECT 1 FROM game.AccountVault WHERE AccountId = @AccountId)
        INSERT INTO game.AccountVault (AccountId) VALUES (@AccountId);
END;
