-- Normalizes legacy `proxyinfo`'s packed aItem VARCHAR(850)/aSocket VARCHAR(775) fixed-width strings into
-- one row per populated sale slot.
-- SLOT COUNT AND WIDTHS ARE FULLY CONFIRMED, not guessed: Protocol/DEFINE.h defines
-- MAX_PSHOP_PAGE_NUM=5 x MAX_PSHOP_SLOT_NUM=5 = 25 slots. ts25extra/S08_MyDB.cpp's save path
-- (CreateIntPad calls) packs each slot as ID(5 digits) + Quantity(3) + Value(9) + Serial(8) + Price(9) = 34
-- chars/slot x 25 slots = 850 -- an exact match for aItem's column width. The same file's GetSocketString
-- (CSQLDatabase.h) advances its write position by exactly 31 chars per slot x 25 slots = 775 -- an exact
-- match for aSocket's column width. SlotIndex 0-24 is PAGE*MAX_PSHOP_SLOT_NUM+SLOT (row-major over the
-- legacy tItem[page][slot] array).
-- DEVIATION FROM THE ORIGINAL TASK BRIEF, DOCUMENTED FOR REVIEW: the brief's suggested shape was just
-- (CharacterId, SlotIndex, ItemId, SocketData). Protocol/STRUCT.h's PROXY_SHOP_ITEM struct
-- (`{ int ID, Quantity, Value, Serial, Price; }`), confirmed byte-for-byte against the pack widths above,
-- shows each slot actually carries 4 more real fields beyond the item id -- most importantly Price, without
-- which a "vending stall" cannot function as a sellable listing at all. Quantity/Value/SerialNumber/Price
-- are added here as real columns rather than silently dropping recoverable, functionally-necessary data;
-- SocketData is kept exactly as briefed (an opaque packed string -- the SOCKET_GEM_V2 bitfield packed into
-- it by GetSocketString is a separate, deeper reverse-engineering task out of scope for this pass).
-- ItemId 0 is not a valid world.Items row -- translate to NULL, same as every other cross-domain FK-shaped
-- column in this schema (an empty sale slot has ItemId=0 in the packed source).
CREATE TABLE game.OfflineShopItems
(
    CharacterId  INT      NOT NULL,
    SlotIndex    SMALLINT NOT NULL,
    ItemId       INT NULL,
    Quantity     INT      NOT NULL CONSTRAINT DF_OfflineShopItems_Quantity DEFAULT 0,
    Value        INT      NOT NULL CONSTRAINT DF_OfflineShopItems_Value DEFAULT 0,
    SerialNumber INT      NOT NULL CONSTRAINT DF_OfflineShopItems_SerialNumber DEFAULT 0,
    Price        INT      NOT NULL CONSTRAINT DF_OfflineShopItems_Price DEFAULT 0,
    SocketData   NVARCHAR(50) NULL,
    CONSTRAINT PK_OfflineShopItems PRIMARY KEY CLUSTERED (CharacterId, SlotIndex),
    CONSTRAINT CK_OfflineShopItems_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 24),
    CONSTRAINT FK_OfflineShopItems_Shop FOREIGN KEY (CharacterId) REFERENCES game.OfflineShops (CharacterId) ON DELETE CASCADE,
    CONSTRAINT FK_OfflineShopItems_Item FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId)
);
