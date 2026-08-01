-- TVP for usp_Character_CreateWithStarterKit: one row per assigned hotkey slot, mirroring
-- game.CharacterHotkeys' own (Page, KeyIndex, Sort, Value1, Value2) shape verbatim.
--
-- Same non-narrowing rationale as game.CharacterHotkeys itself: despite the "Sort" name, Value2 is the
-- actual HotkeyBindingKind byte-range discriminator, not Sort -- all three columns stay INT so this TVP's
-- shape matches the table it feeds exactly.
CREATE TYPE game.tvp_CharacterHotkeySlot AS TABLE
(
    Page     TINYINT NOT NULL,
    KeyIndex TINYINT NOT NULL,
    Sort     INT     NOT NULL,
    Value1   INT     NOT NULL,
    Value2   INT     NOT NULL
);
