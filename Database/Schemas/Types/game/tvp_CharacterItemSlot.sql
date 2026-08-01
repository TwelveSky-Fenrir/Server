-- TVP for usp_CharacterItems_ReplaceContainer: one row per occupied slot of a single container
-- (whole-container replace, mirroring legacy whole-array saves).
-- Quantity stays INT here even though game.CharacterItems.Quantity itself is SMALLINT -- this keeps the
-- write-side [GenerateTvp] record and every one of its call sites int-typed; SQL Server narrows it safely
-- on INSERT (bounded 1-999 by CK_CharacterItems_Quantity anyway). Not a mismatch to "fix".
CREATE TYPE game.tvp_CharacterItemSlot AS TABLE
(
    Slot       TINYINT NOT NULL,
    ItemId     INT     NOT NULL,
    Quantity   INT     NOT NULL,
    Enchant    TINYINT NOT NULL,
    Combine    TINYINT NOT NULL,
    Refine     TINYINT NOT NULL,
    Socket     TINYINT NOT NULL,
    SocketGem1 INT     NOT NULL,
    SocketGem2 INT     NOT NULL,
    SocketGem3 INT     NOT NULL,
    ExpireDate INT     NOT NULL,
    Serial     INT     NOT NULL
);
