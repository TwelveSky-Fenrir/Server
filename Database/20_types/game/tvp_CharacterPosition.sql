-- Read-only shape for usp_Character_PersistBatch's TVP parameter (architecture reference §12.6
-- idempotent write-behind flush): one row per dirty character, FlushSequence carried alongside so
-- the proc can discard any row that is stale vs. game.Characters.FlushSequence (out-of-order network
-- delivery). No PK/index: passed by value per call, never stored, so there is nothing to keyed-lookup.
CREATE TYPE game.tvp_CharacterPosition AS TABLE
    (
    CharacterId INT NOT NULL,
    FlushSequence BIGINT NOT NULL,
    MapId SMALLINT NOT NULL,
    PosX REAL NOT NULL,
    PosY REAL NOT NULL,
    PosZ REAL NOT NULL,
    Heading REAL NOT NULL
    );
