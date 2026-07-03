-- Read-only shape for usp_CharacterItems_ReplaceContainer's TVP parameter: one row per OCCUPIED slot of
-- ONE container (CharacterId/Container are proc scalars, not TVP columns -- the replace unit is a whole
-- container, mirroring how the legacy saves each avatar array as a block, never slot-by-slot; same
-- whole-block rationale as game.tvp_AccountVaultItemSlot). Column order/types mirror
-- game.CharacterItems 1:1 minus the key prefix. No PK/index: passed by value per call, never stored.
CREATE TYPE game.tvp_CharacterItemSlot AS TABLE
    (
    Slot TINYINT NOT NULL,
    ItemId INT NOT NULL,
    Quantity INT NOT NULL,
    Enchant TINYINT NOT NULL,
    Combine TINYINT NOT NULL,
    Refine TINYINT NOT NULL,
    Socket TINYINT NOT NULL,
    SocketGem1 INT NOT NULL,
    SocketGem2 INT NOT NULL,
    SocketGem3 INT NOT NULL,
    ExpireDate INT NOT NULL,
    Serial INT NOT NULL
    );
