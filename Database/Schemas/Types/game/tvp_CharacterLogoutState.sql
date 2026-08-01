-- TVP for usp_CharacterLogoutState_PersistBatch: FlushSequence guards against a replayed/out-of-order
-- write-behind batch against game.CharacterLogoutState's own column, not game.Characters' -- see that
-- table's own header for why the two must not be conflated.
CREATE TYPE game.tvp_CharacterLogoutState AS TABLE
(
    CharacterId   INT    NOT NULL,
    FlushSequence BIGINT NOT NULL,
    LastZone      INT    NOT NULL,
    PosX          INT    NOT NULL,
    PosY          INT    NOT NULL,
    PosZ          INT    NOT NULL,
    Life          INT    NOT NULL,
    Mana          INT    NOT NULL
);
