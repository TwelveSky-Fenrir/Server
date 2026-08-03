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
