-- Legacy: PROXY_NAME_INFO.DAT (CharacterId -> shop display name); this table, not game.OfflineShops.ShopName,
-- is authoritative (that column is legacy-dead in this build).
-- No FK on CharacterId: memory-optimized table can't FK to disk-based game.Characters; enforced by the
-- application (usp_ProxyShopName_Set).
CREATE TABLE game.ProxyShopNames
(
    CharacterId INT          NOT NULL,
    ShopName    NVARCHAR(48) NOT NULL
        CONSTRAINT DF_ProxyShopNames_ShopName DEFAULT N'',
    CONSTRAINT PK_ProxyShopNames PRIMARY KEY NONCLUSTERED HASH (CharacterId) WITH (BUCKET_COUNT = 131072)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_AND_DATA);
