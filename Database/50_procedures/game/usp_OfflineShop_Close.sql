-- database/50_procedures/game/usp_OfflineShop_Close.sql
-- Contract: close (remove) a character's offline shop, e.g. on player-initiated close or logout-cleanup.
-- Params: @CharacterId INT.
-- Result set: none.
-- Idempotent: yes -- closing an already-closed/never-opened shop affects 0 rows and raises no error,
-- matching game.usp_Character_Delete's own idiom. ON DELETE CASCADE (FK_OfflineShopItems_Shop) removes the
-- shop's item slots in the same statement, so no separate item cleanup call is needed.
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
