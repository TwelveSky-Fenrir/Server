-- database/50_procedures/game/usp_ProxyShopName_GetByCharacter.sql
-- Empty result set if the character has never set a proxy-shop name.
CREATE PROCEDURE game.usp_ProxyShopName_GetByCharacter @CharacterId INT
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT CharacterId, ShopName
    FROM game.ProxyShopNames
    WHERE CharacterId = @CharacterId;
END;
