-- Legacy `proxyinfo` (nxtserver.sql) -- NOT legacy network topology, verified as the offline player-vending-
-- stall feature (a character plants a shop, logs off, others browse/buy). Protocol/STRUCT.h's PROXY_OBJECT/
-- PROXY_SHOP_USER_INFO/PROXY_STATE_INFO confirm this exact shape (mZoneNumber/mShopState/mShopDate,
-- tMoney/tBigMoney, pLocation, tItem/tSocket).
-- ShopDate kept as a raw INT (not converted to DATETIME2): the one real sample row in the dump stores
-- 20250930 -- a YYYYMMDD-formatted integer, not a Unix epoch -- so a DATETIME2 conversion would need its own
-- parsing convention with no clear payoff for a low-value bookkeeping field; kept byte-faithful instead.
-- LocationX/Y/Z are INT even though PROXY_STATE_INFO.pLocation is `float[3]` in memory: the legacy DB save
-- path itself truncates to int before writing ((int)r->tState.pLocation[0], ts25extra/S08_MyDB.cpp), so INT
-- is the real persisted precision, not a lossy simplification introduced here.
-- ShopName -- direct inspection of ts25extra/S08_MyDB.cpp's save path shows that in the currently active
-- build (PPSHOP_V2 is defined, Protocol/DEFINE.h) the code path that would populate aShopName from
-- pPShopName is dead (`#ifndef PPSHOP_V2 ... #else pName = ""; #endif` -- the #else always wins), so
-- aShopName is always persisted as an empty string in this build; the sample dump row confirms this
-- (aShopName=''). The real, actively-written shop display name lives in PROXY_NAME_INFO.DAT, modeled here
-- as game.ProxyShopNames -- ShopName is kept on this row for legacy-fidelity/schema completeness only;
-- application code should treat game.ProxyShopNames as authoritative for display purposes.
-- ZoneNumber is NULLable and only loosely enforced via FK (not required NOT NULL) -- world.Zones is a
-- parallel domain's table; a shop's zone reference should not hard-fail if that domain's data isn't seeded
-- yet in some environment.
CREATE TABLE game.OfflineShops
(
    CharacterId INT     NOT NULL,
    ZoneNumber  SMALLINT NULL,
    ShopState   TINYINT NOT NULL CONSTRAINT DF_OfflineShops_ShopState DEFAULT 0,
    ShopDate    INT     NOT NULL CONSTRAINT DF_OfflineShops_ShopDate DEFAULT 0,
    Money       INT     NOT NULL CONSTRAINT DF_OfflineShops_Money DEFAULT 0,
    BigMoney    INT     NOT NULL CONSTRAINT DF_OfflineShops_BigMoney DEFAULT 0,
    LocationX   INT     NOT NULL CONSTRAINT DF_OfflineShops_LocationX DEFAULT 0,
    LocationY   INT     NOT NULL CONSTRAINT DF_OfflineShops_LocationY DEFAULT 0,
    LocationZ   INT     NOT NULL CONSTRAINT DF_OfflineShops_LocationZ DEFAULT 0,
    ShopName    NVARCHAR(48) NOT NULL CONSTRAINT DF_OfflineShops_ShopName DEFAULT N'',
    CONSTRAINT PK_OfflineShops PRIMARY KEY CLUSTERED (CharacterId),
    CONSTRAINT FK_OfflineShops_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId),
    CONSTRAINT FK_OfflineShops_Zone FOREIGN KEY (ZoneNumber) REFERENCES world.Zones (ZoneNumber),
    INDEX       IX_OfflineShops_Zone NONCLUSTERED (ZoneNumber)
);
