-- TVP for usp_Character_PersistRunes: the whole per-character rune-socket array (OCCUPIED sockets only).
-- Mirrors game.CharacterRunes minus CharacterId (a proc scalar) -- the replace unit is the entire 4-socket
-- array (legacy aRuneSystem[4]/aRuneSystemStat[4] save block), the same whole-container-replace shape as
-- game.tvp_CharacterItemSlot. An all-empty rune state sends zero rows: the caller omits this TVP entirely
-- (SQL Server rejects a zero-row TVP) and the procedure's DELETE alone clears the table.
CREATE TYPE game.tvp_CharacterRuneSocket AS TABLE
(
    SocketIndex TINYINT NOT NULL,
    RuneItemId  INT     NOT NULL,
    RuneStat    INT     NOT NULL
);
