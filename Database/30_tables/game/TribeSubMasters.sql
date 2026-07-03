-- DEVIATION FROM THE ORIGINAL TASK BRIEF, DOCUMENTED FOR REVIEW: the brief suggested two fixed columns
-- (SubMaster1CharacterId/SubMaster2CharacterId) directly on game.Tribes, modeled on guildinfo's real
-- 2-slot gSubMaster01/02. Direct inspection of Protocol/STRUCT.h's TRIBE_INFO shows tribes do NOT share
-- that shape: `mTribeSubMaster[MAX_TRIBE_NUM][MAX_TRIBE_SUBMASTER_NUM]` with MAX_TRIBE_SUBMASTER_NUM=12
-- (Protocol/DEFINE.h) -- a real 12-slot-per-tribe array. Per the "normalize, don't transliterate" rule
-- (>4 slots -> child table, one row per populated slot), this is normalized here instead of hardcoding
-- 2 (undercounting) or 12 (mostly-empty) columns on game.Tribes.
-- SlotIndex is the legacy mTribeSubMaster[tribe][0..11] array position; CharacterId replaces the legacy
-- VARCHAR(12) name-per-slot with a real FK, same reasoning as everywhere else in this domain.
-- UNIQUE(TribeId, CharacterId): a character shouldn't occupy two sub-master slots in the same tribe
-- simultaneously (the legacy array has no such guard, but nothing in the game design relies on the
-- absence of one, and allowing it would just be a latent bug surface).
CREATE TABLE game.TribeSubMasters
(
    TribeId     TINYINT NOT NULL,
    SlotIndex   TINYINT NOT NULL,
    CharacterId INT     NOT NULL,
    CONSTRAINT PK_TribeSubMasters PRIMARY KEY CLUSTERED (TribeId, SlotIndex),
    CONSTRAINT UQ_TribeSubMasters_Tribe_Character UNIQUE (TribeId, CharacterId),
    CONSTRAINT CK_TribeSubMasters_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 11),
    CONSTRAINT FK_TribeSubMasters_Tribe FOREIGN KEY (TribeId) REFERENCES game.Tribes (TribeId),
    CONSTRAINT FK_TribeSubMasters_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId)
);
