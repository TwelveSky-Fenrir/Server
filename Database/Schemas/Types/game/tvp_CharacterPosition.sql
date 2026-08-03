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
