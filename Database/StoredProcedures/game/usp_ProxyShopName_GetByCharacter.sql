CREATE PROCEDURE game.usp_ProxyShopName_GetByCharacter @CharacterId INT
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT CharacterId, ShopName
    FROM game.ProxyShopNames
    WHERE CharacterId = @CharacterId;
END;
