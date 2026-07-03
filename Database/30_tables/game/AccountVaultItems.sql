-- Normalizes legacy masterinfo's packed uSaveItem VARCHAR(728)/uSaveSocketGem VARCHAR(756) fixed-width
-- strings into one row per populated vault slot.
-- SLOT COUNT IS A CONFIRMED ENGINE CONSTANT, not a guess: Protocol/DEFINE.h's MAX_SAVE_ITEM_SLOT_NUM=28 is
-- used engine-wide for this exact "save item" concept -- confirmed live in the sibling memberinfo.uSaveItem
-- column (700 chars = 28 slots x 25 chars/slot, format string "%05d%03d%09d%08d" found verbatim in
-- ts25login/S08_MyDB.cpp). masterinfo's own uSaveItem is 728 chars = 28 slots x 26 chars/slot -- same
-- confirmed slot count, a slightly different per-slot sub-width we cannot recover byte-for-byte since no
-- live code path packs/unpacks masterinfo specifically anymore (it is a dead multi-tier mirror per the
-- prior investigation pass) -- only the SLOT COUNT half of this table's shape is fully ground-truthed.
-- DEVIATION FROM THE ORIGINAL TASK BRIEF, DOCUMENTED FOR REVIEW: the brief's suggested shape was just
-- (AccountId, SlotIndex, ItemId, SocketData). memberinfo's confirmed "%05d%03d%09d%08d" format is exactly
-- Protocol/STRUCT.h's PROXY_SHOP_ITEM's first four fields (ID(5), Quantity(3), Value(9), Serial(8), in the
-- same widths) minus its 5th field (Price) -- consistent with a personal bank slot (nothing here is for
-- sale, so no listed price, but the item still carries a value/serial). Quantity/Value/SerialNumber are
-- added here as real columns on that same evidence rather than silently dropping recoverable data; SocketData
-- is kept exactly as briefed (an opaque packed string -- masterinfo's socket width of 27 chars/slot, vs.
-- memberinfo/proxyinfo's confirmed 31-char GetSocketString format, suggests a different/older socket-gem
-- encoding we do not have source for -- decoding it is a separate, deeper reverse-engineering task out of
-- scope for this pass).
-- ItemId 0 is not a valid world.Items row -- translate to NULL, same as every other cross-domain FK-shaped
-- column in this schema (an empty vault slot has ItemId=0 in the packed source).
CREATE TABLE game.AccountVaultItems
(
    AccountId    INT      NOT NULL,
    SlotIndex    SMALLINT NOT NULL,
    ItemId       INT NULL,
    Quantity     INT      NOT NULL CONSTRAINT DF_AccountVaultItems_Quantity DEFAULT 0,
    Value        INT      NOT NULL CONSTRAINT DF_AccountVaultItems_Value DEFAULT 0,
    SerialNumber INT      NOT NULL CONSTRAINT DF_AccountVaultItems_SerialNumber DEFAULT 0,
    SocketData   NVARCHAR(50) NULL,
    CONSTRAINT PK_AccountVaultItems PRIMARY KEY CLUSTERED (AccountId, SlotIndex),
    CONSTRAINT CK_AccountVaultItems_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 27),
    CONSTRAINT FK_AccountVaultItems_Vault FOREIGN KEY (AccountId) REFERENCES game.AccountVault (AccountId) ON DELETE CASCADE,
    CONSTRAINT FK_AccountVaultItems_Item FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId)
);
