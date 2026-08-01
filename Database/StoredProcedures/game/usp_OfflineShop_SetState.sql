-- database/50_procedures/game/usp_OfflineShop_SetState.sql
-- Player-facing close only sets ShopState=0; it does NOT clear items/money (those stay retrievable/
-- withdrawable while closed). A separate full-DELETE admin/account-cleanup procedure
-- (usp_OfflineShop_Close) used to exist but had zero callers anywhere -- removed as dead code in the
-- 2026-07-12 Database/ cleanup pass; a future GM/account-cleanup tool should add its own procedure when
-- that need is real rather than reviving this one speculatively.
CREATE PROCEDURE game.usp_OfflineShop_SetState @CharacterId INT,
                                               @ShopState TINYINT
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
