-- Replaces PROXY_NAME_INFO.DAT, a legacy flat file mapping character id -> proxy-shop display name, saved
-- every 60s + on every set/clear (per this domain's brief). This is the REAL, actively-written shop-name
-- store in the current build -- game.OfflineShops.ShopName (from proxyinfo.aShopName) is legacy-dead weight
-- in this build (see that table's header for the direct source citation showing aShopName is always saved
-- empty), so this table -- not that column -- is authoritative for a shop's display name.
-- MEMORY_OPTIMIZED / SCHEMA_AND_DATA: a hot-ish write path (every 60s + every set/clear across every
-- character with an open shop, though far less frequent than game.TribeBank's per-transaction writes), and
-- names must survive a restart the same way the legacy .DAT file survives a process bounce.
-- HASH index, not the NONCLUSTERED range index game.TribeBank/game.GuildNotices use: access here is pure
-- point lookup by CharacterId (no prefix-scan-by-a-leading-column read pattern the way TribeBank scans all
-- slots for one TribeId), so a HASH index (matching runtime.SessionTickets' own reasoning) is the right fit.
-- NOTE -- no FK on CharacterId to game.Characters: same In-Memory OLTP limitation as game.TribeBank/
-- game.GuildNotices (FOREIGN KEY on a memory-optimized table is only supported when the referenced table is
-- ALSO memory-optimized, and game.Characters is a regular disk-based table). Referential correctness is
-- guaranteed by construction instead: the application is the sole writer of this table via
-- usp_ProxyShopName_Set, and a CharacterId only ever originates from game.Characters.CharacterId.
-- Starts empty (no existing players yet) -- schema only, no seed, per this domain's brief.
CREATE TABLE game.ProxyShopNames
(
    CharacterId INT NOT NULL,
    ShopName    NVARCHAR(48) NOT NULL CONSTRAINT DF_ProxyShopNames_ShopName DEFAULT N'',
    CONSTRAINT PK_ProxyShopNames PRIMARY KEY NONCLUSTERED HASH (CharacterId) WITH (BUCKET_COUNT = 131072)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_AND_DATA);
