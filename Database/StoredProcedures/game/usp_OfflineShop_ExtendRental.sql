-- database/50_procedures/game/usp_OfflineShop_ExtendRental.sql
-- Writes a freshly computed ShopDate (proxy-shop rental expiration) for the proxy-shop rental-extension
-- consumables (world.Items 567/592/8422/8423, CZ_USE_INVENTORY_ITEM_SEND). Deliberately no @@ROWCOUNT
-- guard -- a character with no game.OfflineShops row yet still "succeeds" with nothing persisted, matching
-- the legacy's own usp-equivalent (Server/ts25extra/S08_MyDB.cpp:1085-1106 returns success both when a row
-- was updated and when none matched).
CREATE PROCEDURE game.usp_OfflineShop_ExtendRental @CharacterId INT,
    @ShopDate    INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE game.OfflineShops
    SET ShopDate = @ShopDate
    WHERE CharacterId = @CharacterId;
END;
