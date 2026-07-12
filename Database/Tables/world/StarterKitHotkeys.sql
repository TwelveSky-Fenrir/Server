-- Legacy: same function's aHotKey[0][0..2] block. Only page 0 / keys 0-2 are ever populated at creation
-- (pages 1-2 and the remaining keys of page 0 start empty); Sort/Value1/Value2 stored verbatim as the legacy
-- triple, same convention as game.CharacterHotkeys.
CREATE TABLE world.StarterKitHotkeys
(
    PreviousTribe TINYINT NOT NULL,
    Page          TINYINT NOT NULL,
    KeyIndex      TINYINT NOT NULL,
    Sort          INT     NOT NULL,
    Value1        INT     NOT NULL,
    Value2        INT     NOT NULL,
    CONSTRAINT PK_StarterKitHotkeys PRIMARY KEY CLUSTERED (PreviousTribe, Page, KeyIndex),
    CONSTRAINT CK_StarterKitHotkeys_PreviousTribe CHECK (PreviousTribe BETWEEN 0 AND 2),
    -- Page/KeyIndex bounds mirror the same legacy array dimensions already cited on the sibling
    -- game.CharacterHotkeys table: wAvatar.aHotKey[MAX_HOT_KEY_PAGE][MAX_HOT_KEY_NUM][3], i.e. 3 pages
    -- (Server/Header/Protocol/STRUCT.h:365) x 14 keys/page -- only page 0 / keys 0-2 are ever populated by
    -- this seed (see header comment), but the storage-level bound should match the real array shape, not
    -- just the currently-seeded subset.
    CONSTRAINT CK_StarterKitHotkeys_Page CHECK (Page BETWEEN 0 AND 2),
    CONSTRAINT CK_StarterKitHotkeys_KeyIndex CHECK (KeyIndex BETWEEN 0 AND 13)
);
