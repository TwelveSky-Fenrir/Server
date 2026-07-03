-- database/50_procedures/game/usp_AccountVault_Get.sql
-- Contract: @AccountId -> 2 result sets, called on account/vault-panel open to load the whole vault in one
-- round trip:
--   RS0: the game.AccountVault row (0 or 1 rows -- 0 until game.usp_AccountVault_EnsureInitialized has run
--        for this account)
--   RS1: one row per game.AccountVaultItems slot (0-28 rows), ordered by SlotIndex
-- Read-only, safe to retry.
CREATE PROCEDURE game.usp_AccountVault_Get @AccountId INT
AS
BEGIN
    SET
NOCOUNT ON;

SELECT AccountId, Money, Money2, UpdatedAtUtc
FROM game.AccountVault
WHERE AccountId = @AccountId;

SELECT SlotIndex, ItemId, Quantity, Value, SerialNumber, SocketData
FROM game.AccountVaultItems
WHERE AccountId = @AccountId
ORDER BY SlotIndex;
END;
