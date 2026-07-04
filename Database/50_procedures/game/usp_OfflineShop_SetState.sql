-- database/50_procedures/game/usp_OfflineShop_SetState.sql
-- Player-facing close only sets ShopState=0; it does NOT clear items/money (those stay retrievable/
-- withdrawable while closed). usp_OfflineShop_Close (full DELETE) is admin/account-cleanup only.
CREATE PROCEDURE game.usp_OfflineShop_SetState @CharacterId INT,
    @ShopState   TINYINT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

UPDATE game.OfflineShops
SET ShopState = @ShopState
WHERE CharacterId = @CharacterId;
END;
