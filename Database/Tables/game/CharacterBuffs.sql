-- Legacy: aBufSort/aBufEffectValue/aBufEffectTime arrays; one row per occupied buff slot.
-- IMPORTANT CALLER CONTRACT: the legacy client always zeroes uBuffInfo on a fresh login before world-entry
-- (RegisterUserForLogin), so these rows must NOT be reapplied on normal Login->CharSelect->world-entry --
-- usp_Character_GetForWorldEntry.RS4 has no legitimate consumer today; this table exists for a future
-- process-restart-resume scenario only.
-- RemainingLegacyTicks is stored in legacy ticks (500ms/tick), not ms, to avoid conversion drift.
CREATE TABLE game.CharacterBuffs
(
    CharacterId          INT     NOT NULL,
    SlotIndex            TINYINT NOT NULL,
    Value                INT     NOT NULL
        CONSTRAINT DF_CharacterBuffs_Value DEFAULT 0,
    RemainingLegacyTicks INT     NOT NULL
        CONSTRAINT DF_CharacterBuffs_RemainingLegacyTicks DEFAULT 0,
    CONSTRAINT PK_CharacterBuffs PRIMARY KEY CLUSTERED (CharacterId, SlotIndex),
    CONSTRAINT CK_CharacterBuffs_SlotIndex CHECK (SlotIndex <= 34), -- 35 legacy buff slots (MG5ORIGIN)
    CONSTRAINT CK_CharacterBuffs_RemainingLegacyTicks CHECK (RemainingLegacyTicks >= 0),
    CONSTRAINT FK_CharacterBuffs_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId)
);
