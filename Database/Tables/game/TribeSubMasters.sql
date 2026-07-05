-- Legacy: mTribeSubMaster[tribe][0..11] (MAX_TRIBE_SUBMASTER_NUM=12); SlotIndex is the raw array position,
-- CharacterId replaces the legacy per-slot name.
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
