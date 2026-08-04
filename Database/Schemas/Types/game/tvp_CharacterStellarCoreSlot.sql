CREATE TYPE game.tvp_CharacterStellarCoreSlot AS TABLE
(
    CharacterId INT     NOT NULL,
    Slot        TINYINT NOT NULL,
    ItemId      INT     NOT NULL
);
