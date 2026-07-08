-- Normalizes the legacy per-avatar item arrays (aInventory[2][64][6], aEquip[13][4], aStoreItem[2][28][4]
-- + parallel socket/expiry arrays) into one row per actually-occupied slot; row absence = empty slot.
-- Container enum: 0/1 = InventoryPage0/1 (slots 0-63), 2 = Equipment (slots 0-12), 3/4 = StorePage0/1
-- (slots 0-27; page access gated by Characters.InventoryDate/StoreDate respectively).
-- Trade and the account vault (game.AccountVaultItems) are deliberately not containers here.
-- ExpireDate is the rental expiry as legacy int YYYYMMDD (0 = not a rental), stored verbatim since the
-- same int travels to the client in add-item packets.
CREATE TABLE game.CharacterItems
(
    CharacterId INT     NOT NULL,
    Container   TINYINT NOT NULL,
    Slot        TINYINT NOT NULL,
    ItemId      INT     NOT NULL,
    Quantity    INT     NOT NULL
        CONSTRAINT DF_CharacterItems_Quantity DEFAULT 1,
    Enchant     TINYINT NOT NULL
        CONSTRAINT DF_CharacterItems_Enchant DEFAULT 0,
    Combine     TINYINT NOT NULL
        CONSTRAINT DF_CharacterItems_Combine DEFAULT 0,
    Refine      TINYINT NOT NULL
        CONSTRAINT DF_CharacterItems_Refine DEFAULT 0,
    Socket      TINYINT NOT NULL
        CONSTRAINT DF_CharacterItems_Socket DEFAULT 0,
    SocketGem1  INT     NOT NULL
        CONSTRAINT DF_CharacterItems_SocketGem1 DEFAULT 0,
    SocketGem2  INT     NOT NULL
        CONSTRAINT DF_CharacterItems_SocketGem2 DEFAULT 0,
    SocketGem3  INT     NOT NULL
        CONSTRAINT DF_CharacterItems_SocketGem3 DEFAULT 0,
    ExpireDate  INT     NOT NULL
        CONSTRAINT DF_CharacterItems_ExpireDate DEFAULT 0,
    Serial      INT     NOT NULL
        CONSTRAINT DF_CharacterItems_Serial DEFAULT 0,
    CONSTRAINT PK_CharacterItems PRIMARY KEY CLUSTERED (CharacterId, Container, Slot),
    CONSTRAINT CK_CharacterItems_Quantity CHECK (Quantity BETWEEN 1 AND 999), -- MAX_ITEM_DUPLICATION_NUM (Server/Header/Protocol/DEFINE.h:611); schema-level backstop, not a substitute for ContainerMatrix.ResolveMove's own merge-cap check
    CONSTRAINT CK_CharacterItems_ContainerSlot CHECK (
        (Container IN (0, 1) AND Slot <= 63)          -- inventory pages, 8x8
            OR (Container = 2 AND Slot <= 12)         -- equipment, MAX_EQUIP_SLOT_NUM = 13
            OR (Container IN (3, 4) AND Slot <= 27)), -- store pages, MAX_STORE_ITEM_SLOT_NUM = 28
    CONSTRAINT FK_CharacterItems_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId),
    CONSTRAINT FK_CharacterItems_Item FOREIGN KEY (ItemId) REFERENCES world.Items (ItemId),
    INDEX IX_CharacterItems_ItemId NONCLUSTERED (ItemId)
);
