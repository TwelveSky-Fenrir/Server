-- TVP for usp_Character_CreateWithStarterKit: one row per assigned hotkey slot, mirroring
-- game.CharacterHotkeys' own (Page, KeyIndex, Sort, Value1, Value2) shape verbatim.
CREATE TYPE game.tvp_CharacterHotkeySlot AS TABLE
(
    Page     TINYINT NOT NULL,
    KeyIndex TINYINT NOT NULL,
    Sort     INT     NOT NULL,
    Value1   INT     NOT NULL,
    Value2   INT     NOT NULL
);
