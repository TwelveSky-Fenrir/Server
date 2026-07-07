-- Legacy: wAvatar.aHotKey[3][14][3] (3 pages x 14 keys x 3 ints); row absence = unassigned key.
-- Sort/Value1/Value2 stored verbatim as the legacy triple; per-sort payload semantics belong to hotkey
-- logic, not storage.
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
