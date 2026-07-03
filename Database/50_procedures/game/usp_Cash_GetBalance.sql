-- database/50_procedures/game/usp_Cash_GetBalance.sql
-- Contract: read one account's cash balance (legacy MemberInfo.uCash read, cash-shop opcode 14 --
-- report 06 §3.2). Always returns exactly one row: an account with no game.AccountCash row has balance
-- 0 (rows are only minted by the first credit) -- the caller never needs a null branch for "never
-- bought cash".
-- Params:
--   @AccountId INT
-- Result set (RS0, exactly one row): Balance INT.
-- Idempotent: yes (read-only).
-- Errors: none.
CREATE PROCEDURE game.usp_Cash_GetBalance @AccountId INT
AS
BEGIN
    SET
NOCOUNT ON;

SELECT ISNULL((SELECT Balance
               FROM game.AccountCash
               WHERE AccountId = @AccountId), 0) AS Balance;
END;
