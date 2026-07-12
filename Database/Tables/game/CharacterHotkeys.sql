-- Legacy: wAvatar.aHotKey[3][14][3] (3 pages x 14 keys x 3 ints); row absence = unassigned key.
-- Sort/Value1/Value2 stored verbatim as the legacy triple; per-sort payload semantics belong to hotkey
-- logic, not storage.
--
-- Deliberately all three stay INT, not narrowed: Sort holds the bound skill/item id, Value1 the secondary
-- value (grade/quantity), and Value2 -- despite the "Sort" column name -- is the actual HotkeyBindingKind
-- discriminator (see usp_CharacterHotkeys_UpsertSlot.sql's header for the exact per-position mapping).
-- Value2's real range (HotkeyBindingKind: 0-3) would fit TINYINT, but narrowing only that one column would
-- bake a column-position-to-meaning assumption into storage that this table intentionally avoids.
CREATE TABLE game.CharacterHotkeys
(
    CharacterId INT     NOT NULL,
    Page        TINYINT NOT NULL,
    KeyIndex    TINYINT NOT NULL,
    Sort        INT     NOT NULL,
    Value1      INT     NOT NULL
        CONSTRAINT DF_CharacterHotkeys_Value1 DEFAULT 0,
    Value2      INT     NOT NULL
        CONSTRAINT DF_CharacterHotkeys_Value2 DEFAULT 0,
    CONSTRAINT PK_CharacterHotkeys PRIMARY KEY CLUSTERED (CharacterId, Page, KeyIndex),
    CONSTRAINT CK_CharacterHotkeys_Page CHECK (Page <= 2),          -- aHotKey[3]
    CONSTRAINT CK_CharacterHotkeys_KeyIndex CHECK (KeyIndex <= 13), -- aHotKey[][14]
    CONSTRAINT FK_CharacterHotkeys_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId)
);
