-- database/50_procedures/game/usp_ProxyShopName_Set.sql
-- Replaces PROXY_NAME_INFO.DAT's save-on-set/clear path; an empty string is a valid "name cleared" value.
CREATE PROCEDURE game.usp_ProxyShopName_Set @CharacterId INT,
                                            @ShopName NVARCHAR(48)
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DELETE
    FROM game.ProxyShopNames
    WHERE CharacterId = @CharacterId;

    INSERT INTO game.ProxyShopNames (CharacterId, ShopName)
    VALUES (@CharacterId, @ShopName);
END;
