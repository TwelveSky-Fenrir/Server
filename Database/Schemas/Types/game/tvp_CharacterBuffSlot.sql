CREATE TYPE game.tvp_CharacterBuffSlot AS TABLE
(
    CharacterId          INT     NOT NULL,
    SlotIndex            TINYINT NOT NULL,
    Value                INT     NOT NULL,
    RemainingLegacyTicks INT     NOT NULL
);
