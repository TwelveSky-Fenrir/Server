CREATE TYPE game.tvp_CharacterCostumeSlot AS TABLE
(
    CharacterId INT     NOT NULL,
    Slot        TINYINT NOT NULL,
    ItemId      INT     NOT NULL,
    ItemValue   INT     NOT NULL,
    ExpireDate  INT     NOT NULL
);
