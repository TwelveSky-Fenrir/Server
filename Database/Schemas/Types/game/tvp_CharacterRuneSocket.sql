CREATE TYPE game.tvp_CharacterRuneSocket AS TABLE
(
    SocketIndex TINYINT NOT NULL,
    RuneItemId  INT     NOT NULL,
    RuneStat    INT     NOT NULL
);
