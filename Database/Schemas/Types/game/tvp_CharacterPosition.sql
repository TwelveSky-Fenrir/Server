-- TVP for usp_Character_PersistBatch: one row per dirty character; proc discards rows whose
-- FlushSequence is stale vs. game.Characters.FlushSequence (out-of-order delivery).
CREATE TYPE game.tvp_CharacterPosition AS TABLE
(
    CharacterId   INT      NOT NULL,
    FlushSequence BIGINT   NOT NULL,
    MapId         SMALLINT NOT NULL,
    PosX          REAL     NOT NULL,
    PosY          REAL     NOT NULL,
    PosZ          REAL     NOT NULL,
    Heading       REAL     NOT NULL
);
