CREATE TYPE game.tvp_CharacterHotkeySlot AS TABLE
(
    Page     TINYINT NOT NULL,
    KeyIndex TINYINT NOT NULL,
    Sort     INT     NOT NULL,
    Value1   INT     NOT NULL,
    Value2   INT     NOT NULL
);
