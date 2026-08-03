CREATE PROCEDURE game.usp_OfflineShop_ExtendRental @CharacterId INT,
                                                   @ShopDate INT
AS
BEGIN
    SET
        NOCOUNT ON;

    UPDATE game.OfflineShops
    SET ShopDate = @ShopDate
    WHERE CharacterId = @CharacterId;
END;
