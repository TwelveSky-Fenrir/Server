-- Normalizes the legacy hotkey bar wAvatar.aHotKey[3][14][3] (wire AvatarInfo.HotKey[126] = 3 pages x
-- 14 keys x 3 ints) into one row per actually-assigned key -- row-absence = unassigned key, same rule
-- as the other per-slot character tables.
-- The 3 ints per key are stored verbatim as Sort/Value1/Value2: [0] = binding sort (HK_SKILL / HK_EMO /
-- inventory-item binding -- the PROCESS_DATA tSort 204/205/211/253/214/216 family moves entries between
-- wHotkey and the other containers, report 04), [1]/[2] = the sort-dependent payload (skill number +
-- grade, or inventory page + index...). Their per-sort semantics belong to the hotkey logic (Phase C);
-- persisting the raw triple keeps this table a faithful journal of what the wire round-trips.
CREATE TABLE game.CharacterHotkeys
(
    CharacterId INT     NOT NULL,
    Page        TINYINT NOT NULL,
    KeyIndex    TINYINT NOT NULL,
    Sort        INT     NOT NULL,
    Value1      INT     NOT NULL CONSTRAINT DF_CharacterHotkeys_Value1 DEFAULT 0,
    Value2      INT     NOT NULL CONSTRAINT DF_CharacterHotkeys_Value2 DEFAULT 0,
    CONSTRAINT PK_CharacterHotkeys PRIMARY KEY CLUSTERED (CharacterId, Page, KeyIndex),
    CONSTRAINT CK_CharacterHotkeys_Page CHECK (Page <= 2),          -- aHotKey[3]
    CONSTRAINT CK_CharacterHotkeys_KeyIndex CHECK (KeyIndex <= 13), -- aHotKey[][14]
    CONSTRAINT FK_CharacterHotkeys_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId)
);
