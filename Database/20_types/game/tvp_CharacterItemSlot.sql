-- TVP for usp_CharacterItems_ReplaceContainer: one row per occupied slot of a single container
-- (whole-container replace, mirroring legacy whole-array saves).
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
