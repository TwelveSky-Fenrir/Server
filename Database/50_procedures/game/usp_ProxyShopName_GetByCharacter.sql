-- database/50_procedures/game/usp_ProxyShopName_GetByCharacter.sql
-- Contract: @CharacterId -> RS0 { CharacterId, ShopName } | empty result set if the character has never set
-- a proxy-shop name.
-- Read-only, safe to retry. Plain (not natively compiled) proc reading the memory-optimized
-- game.ProxyShopNames table -- a plain proc can freely read a memory-optimized table; only the write path
-- needed native compilation for the hot-ish upsert.
CREATE PROCEDURE game.usp_ProxyShopName_GetByCharacter @CharacterId INT
AS
BEGIN
    SET
NOCOUNT ON;

SELECT CharacterId, ShopName
FROM game.ProxyShopNames
WHERE CharacterId = @CharacterId;
END;
