-- Legacy `tribeinfo` is a real fixed-4-row table (MAX_TRIBE_NUM=4); TribeId is stored as-given (0-3),
-- never IDENTITY. MasterCharacterId replaces the legacy VARCHAR name column with a real FK.
-- Sub-masters are not columns here (see game.TribeSubMasters, a real 12-slot array unlike a guild's 2).
-- Points lives in game.WorldStateTribes (its true legacy home is WORLD_INFO, not tribeinfo), not here.
CREATE TABLE game.Tribes
(
    TribeId           TINYINT NOT NULL,
    MasterCharacterId INT     NULL,
    CONSTRAINT PK_Tribes PRIMARY KEY CLUSTERED (TribeId),
    CONSTRAINT CK_Tribes_TribeId CHECK (TribeId BETWEEN 0 AND 3),
    CONSTRAINT FK_Tribes_MasterCharacter FOREIGN KEY (MasterCharacterId) REFERENCES game.Characters (CharacterId)
);
