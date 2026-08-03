CREATE TYPE game.tvp_CharacterMountSlot AS TABLE
(
    CharacterId INT     NOT NULL,
    Slot        TINYINT NOT NULL,
    ItemId      INT     NOT NULL,
    ExpActivity INT     NOT NULL,
    Power       INT     NOT NULL
);
