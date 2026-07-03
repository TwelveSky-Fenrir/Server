-- Normalizes the legacy per-avatar item arrays (STRUCT.h wAvatar: aInventory[2][64][6], aEquip[13][4],
-- aStoreItem[2][28][4] + the parallel socket/expiry arrays aInvenSocketGem/aEquipSocketGem/aStoreSocketGem
-- [slots][3] and aInvenExpireDate/aEquipExpireDate/aStoreExpireDate) into ONE row per actually-OCCUPIED
-- slot -- "normalize, don't transliterate" (same rationale as game.GuildMembers): most slots of a typical
-- character are empty, a row simply not existing IS "slot empty".
-- Container is a documented enum, one value per legacy array/page:
--   0 = InventoryPage0 (aInventory page 0, slots 0-63, the 8x8 grid)
--   1 = InventoryPage1 (aInventory page 1, slots 0-63; page access gated by Characters.InventoryDate)
--   2 = Equipment      (aEquip, slots 0-12, MAX_EQUIP_SLOT_NUM = 13)
--   3 = StorePage0     (aStoreItem page 0, slots 0-27, MAX_STORE_ITEM_SLOT_NUM = 28)
--   4 = StorePage1     (aStoreItem page 1, slots 0-27; page access gated by Characters.StoreDate --
--                       [CORRIGÉ-REVUE] Store is `aStoreItem[MAX_STORE_ITEM_PAGE_NUM=2][28][4]`
--                       (DEFINE.h:294-296), the SAME two-page/date-gated shape as Inventory, not a flat
--                       56-slot warehouse as an earlier pass modeled it -- split into two page-containers
--                       for the same reason Inventory already is, instead of an undocumented asymmetry.
--                       Its money pools live on game.Characters.StoreMoney/BigStoreMoney.)
-- Deliberately NOT containers here: Trade (transient, never persisted -- legacy loses in-flight trade
-- state on crash by design, the commit writes back into inventory) and the account vault / Save
-- (uSaveItem is ACCOUNT-scoped -> game.AccountVaultItems already owns it).
-- Enchant/Combine/Refine/Socket are the 4 upgrade bytes the legacy packs into the aEquip[slot] value
-- ints -- stored as separate TINYINT columns, never a packed int (task brief; packed ints in SQL are
-- write-only data).
-- SocketGem1..3: numbered columns, not a child table -- a socket block is a fixed, always-exactly-3
-- array (a*SocketGem[slot][3], wire InvenSocket = 128*3 / EquipSocket = 13*3 / StoreSocket = 2*28*3),
-- never sparse in shape, exactly the "small, always-meaningful fixed pairs/triples get numbered columns"
-- rule world.Items already applies to EquipInfo[2]/CapeInfo[3]; and world-entry loads every row of this
-- table on the hot path, where a second child-table join buys nothing. 0 = empty socket (legacy idiom).
-- ExpireDate is the rental expiry, legacy int YYYYMMDD (ReturnNowDate format, report 12 §3: rental ids
-- 76500-76540, wiped at world entry when expireDate <= today); 0 = not a rental. Stored verbatim as INT,
-- not DATETIME2: the same YYYYMMDD int travels to the client in add-item packets (tExpire), so this IS
-- the wire value, not a timestamp to convert.
-- Serial mirrors the legacy per-item serial number (dupe tracking; same role as
-- game.AccountVaultItems.SerialNumber), 0 = none assigned.
-- ItemId is NOT NULL (a row only exists for an occupied slot) and a real FK to world.Items -- the legacy
-- "0 = empty slot" sentinel translates to row-absence, never to a 0 FK.
CREATE TABLE game.CharacterItems
(
    CharacterId INT     NOT NULL,
    Container   TINYINT NOT NULL,
    Slot        TINYINT NOT NULL,
    ItemId      INT     NOT NULL,
    Quantity    INT     NOT NULL CONSTRAINT DF_CharacterItems_Quantity DEFAULT 1,
    Enchant     TINYINT NOT NULL CONSTRAINT DF_CharacterItems_Enchant DEFAULT 0,
    Combine     TINYINT NOT NULL CONSTRAINT DF_CharacterItems_Combine DEFAULT 0,
    Refine      TINYINT NOT NULL CONSTRAINT DF_CharacterItems_Refine DEFAULT 0,
    Socket      TINYINT NOT NULL CONSTRAINT DF_CharacterItems_Socket DEFAULT 0,
    SocketGem1  INT     NOT NULL CONSTRAINT DF_CharacterItems_SocketGem1 DEFAULT 0,
    SocketGem2  INT     NOT NULL CONSTRAINT DF_CharacterItems_SocketGem2 DEFAULT 0,
    SocketGem3  INT     NOT NULL CONSTRAINT DF_CharacterItems_SocketGem3 DEFAULT 0,
    ExpireDate  INT     NOT NULL CONSTRAINT DF_CharacterItems_ExpireDate DEFAULT 0,
    Serial      INT     NOT NULL CONSTRAINT DF_CharacterItems_Serial DEFAULT 0,
    CONSTRAINT PK_CharacterItems PRIMARY KEY CLUSTERED (CharacterId, Container, Slot),
    CONSTRAINT CK_CharacterItems_Quantity CHECK (Quantity >= 1),
    CONSTRAINT CK_CharacterItems_ContainerSlot CHECK (
        (Container IN (0, 1) AND Slot <= 63)          -- inventory pages, 8x8
            OR (Container = 2 AND Slot <= 12)         -- equipment, MAX_EQUIP_SLOT_NUM = 13
            OR (Container IN (3, 4) AND Slot <= 27)), -- store pages, MAX_STORE_ITEM_SLOT_NUM = 28
    CONSTRAINT FK_CharacterItems_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId),
    CONSTRAINT FK_CharacterItems_Item FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId)
);
