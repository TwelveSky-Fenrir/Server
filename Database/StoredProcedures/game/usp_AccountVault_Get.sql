-- RS0 (AccountVault) is empty until the account's vault row has been auto-created by one of the money write
-- paths (usp_AccountVault_TransferMoneyWithCharacter/TransferItemWithCharacter/TransferBigMoneyWithCharacter,
-- usp_Gift_ClaimIntoVault -- each auto-creates the row inline on first use, "IF NOT EXISTS ... INSERT", so a
-- deposit or claim never requires the vault panel to have been opened first). A standalone
-- usp_AccountVault_EnsureInitialized bootstrap procedure used to exist but had zero callers anywhere -- every
-- live money write path already inlines the identical idempotent bootstrap -- removed as dead code in the
-- 2026-07-12 Database/ cleanup pass. This RS0 legitimately returns an empty result set (not an error) for an
-- account that has genuinely never touched the vault in any way. Note usp_AccountVault_SetItems itself does
-- NOT auto-create the row (it only touches AccountVaultItems, which FKs to AccountVault) -- it relies on a
-- prior Get/Transfer having already established one.
CREATE PROCEDURE game.usp_AccountVault_Get @AccountId INT
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT AccountId, Money, Money2, UpdatedAtUtc, BigMoney
    FROM game.AccountVault
    WHERE AccountId = @AccountId;

    SELECT SlotIndex, ItemId, Quantity, Value, SerialNumber, SocketData
    FROM game.AccountVaultItems
    WHERE AccountId = @AccountId
    ORDER BY SlotIndex;
END;
