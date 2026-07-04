-- database/50_procedures/game/usp_OfflineShop_Close.sql
-- Idempotent full delete; ON DELETE CASCADE also removes the shop's item slots.
CREATE PROCEDURE game.usp_OfflineShop_Close @CharacterId INT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

DELETE
FROM game.OfflineShops
WHERE CharacterId = @CharacterId;
END;
