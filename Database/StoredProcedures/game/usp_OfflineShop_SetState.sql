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
