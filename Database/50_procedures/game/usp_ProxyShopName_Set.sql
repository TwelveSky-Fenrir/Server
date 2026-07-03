-- database/50_procedures/game/usp_ProxyShopName_Set.sql
-- Contract: upsert one character's proxy-shop display name (replaces PROXY_NAME_INFO.DAT's save-every-60s +
-- save-on-every-set/clear path -- see game.ProxyShopNames).
-- Params: @CharacterId INT, @ShopName NVARCHAR(48) -- an empty string is a valid, deliberate "shop closed/
-- name cleared" (matching game.usp_GuildNotice_Set's own "empty string is valid" idiom).
-- Result set: none.
-- Idempotent: yes -- setting the same name repeatedly is a no-op in effect.
-- DELETE-then-INSERT, never MERGE (matches game.usp_GuildNotice_Set's idiom exactly): the DELETE is a no-op
-- the first time a given CharacterId is ever set.
-- Natively compiled against game.ProxyShopNames (memory-optimized, SCHEMA_AND_DATA): matches that table's
-- own hot-ish-write-path rationale.
CREATE PROCEDURE game.usp_ProxyShopName_Set @CharacterId INT,
    @ShopName    NVARCHAR(48)
WITH NATIVE_COMPILATION, SCHEMABINDING
AS
BEGIN ATOMIC
WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
DELETE
FROM game.ProxyShopNames
WHERE CharacterId = @CharacterId;

INSERT INTO game.ProxyShopNames (CharacterId, ShopName)
VALUES (@CharacterId, @ShopName);
END;
