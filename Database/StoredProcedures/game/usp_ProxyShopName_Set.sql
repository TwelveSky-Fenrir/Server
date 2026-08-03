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
