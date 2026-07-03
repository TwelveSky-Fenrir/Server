-- Legacy `tribeinfo` (nxtserver.sql) is a real fixed-4-row table (PRIMARY KEY wIndex, MAX_TRIBE_NUM=4,
-- Protocol/DEFINE.h) -- TribeId is stored as-given (0-3), never IDENTITY, matching the domain's own PK
-- contract note. MasterCharacterId replaces the legacy mTribeXXMaster VARCHAR(12) *name* column with a
-- real FK, same reasoning as game.Guilds.MasterCharacterId.
-- Sub-masters are NOT columns here (see game.TribeSubMasters): Protocol/STRUCT.h's TRIBE_INFO defines
-- `mTribeSubMaster[MAX_TRIBE_NUM][MAX_TRIBE_SUBMASTER_NUM]` with MAX_TRIBE_SUBMASTER_NUM=12 (Protocol/
-- DEFINE.h) -- a real 12-slot array, not the 2-slot shape a guild has (guildinfo's gSubMaster01/02 are
-- genuinely only 2 columns; tribes are not the same shape, don't transliterate one onto the other).
-- Points has no tribeinfo column of its own -- the legacy per-tribe point total actually lives in the
-- separate WORLD_INFO aggregate (mTribePoint[MAX_TRIBE_NUM], guarded by #ifndef USE_NEW_CLAN_POINT in
-- CSQLWorldTribe.cpp), which is world/global server state outside this domain's 4 named tables. A
-- parallel pass built exactly that missing world-state table (game.WorldStateTribes, whose own header
-- documents this same field) -- Points lives there now, as the semantically correct home; not
-- duplicated here.
CREATE TABLE game.Tribes
(
    TribeId           TINYINT NOT NULL,
    MasterCharacterId INT NULL,
    CONSTRAINT PK_Tribes PRIMARY KEY CLUSTERED (TribeId),
    CONSTRAINT CK_Tribes_TribeId CHECK (TribeId BETWEEN 0 AND 3),
    CONSTRAINT FK_Tribes_MasterCharacter FOREIGN KEY (MasterCharacterId) REFERENCES game.Characters (CharacterId)
);
