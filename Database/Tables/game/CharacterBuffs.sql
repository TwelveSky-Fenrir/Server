-- Legacy: aBufSort/aBufEffectValue/aBufEffectTime arrays; one row per occupied buff slot.
-- LEGACY CITATION: ServerDocs/13_ts25playuser/01_Session_Character_Broker.md:172-188 (Server/ts25playuser/
-- S07_MyGame01.cpp:973-1013, RegisterUserForLogin_03) -- BUFF_INFO is ZeroMemory'd unconditionally at
-- character-selection, before the round-trip to Zone, on every normal Login->CharSelect->world-entry; these
-- rows are not the legacy source of BuffInfo/EffectValueForView on that path.
-- CURRENT STATE (2026-07-12): CharacterRepository.GetWorldEntryBundleAsync's 5th result set is nonetheless
-- read and reapplied by EnterWorldService.BuildBuffInfo/BuildEffectValueForView on every normal world-entry
-- (wired since commit 26aa0d85), which appears to diverge from the citation above. Flagged for
-- legacy-behavior-translator/fenrir-gameplay-domain-engineer review, not resolved here -- this schema does
-- not unilaterally remove a projection an already-wired Infrastructure/Application contract depends on.
-- Until that's settled, this table remains the durable store for a future crash/restart-resume scenario.
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
