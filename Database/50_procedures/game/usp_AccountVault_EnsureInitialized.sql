-- database/50_procedures/game/usp_AccountVault_EnsureInitialized.sql
-- Contract: idempotent bootstrap -- creates an all-zero game.AccountVault row for @AccountId the first time
-- it is ever called (e.g. on first login/first vault access for that account); a no-op on every later call.
-- Params: @AccountId INT.
-- Result set: none. Idempotent: yes.
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
