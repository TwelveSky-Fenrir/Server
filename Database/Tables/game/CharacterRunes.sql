-- Legacy: wAvatar.aRuneSystem[4] (per-socket rune item id) + wAvatar.aRuneSystemStat[4] (per-socket packed
-- ISUIM stat), Server/Header/Protocol/STRUCT.h:572-573. Persisted here as one normalized row per OCCUPIED
-- socket (row absence = empty socket, 0 sentinel), mirroring game.CharacterSkills' own "row absence = empty
-- slot" convention. Rare, player-initiated, transactional state -- a rune equip/unequip mutates ONE socket at
-- a time, each atomic with an inventory mutation (Server/ts25zone/S04_MyWork03.cpp:8162-8328) -- so it is
-- written by the dedicated transactional usp_Character_PersistRunes, NEVER by the per-tick write-behind
-- position/progress flush.
--
-- RuneItemId is stored verbatim from what usp_Character_PersistRunes is handed: the legacy equip path writes
-- the CLIENT-supplied claimed rune id into the socket without cross-checking it against the item actually
-- consumed (Server/ts25zone/S04_MyWork03.cpp:8223, flagged for cpp-security-debt-auditor as an integrity gap).
-- Reproducing that faithfully means NO FK to world.Items and no rune-family (93514-93517) range CHECK here --
-- either would reject a claimed id the legacy accepts; validation is the caller's job, not the schema's.
CREATE TABLE game.CharacterRunes
(
    CharacterId INT     NOT NULL,
    SocketIndex TINYINT NOT NULL,
    RuneItemId  INT     NOT NULL,
    RuneStat    INT     NOT NULL
        CONSTRAINT DF_CharacterRunes_RuneStat DEFAULT 0,
    CONSTRAINT PK_CharacterRunes PRIMARY KEY CLUSTERED (CharacterId, SocketIndex),
    CONSTRAINT CK_CharacterRunes_SocketIndex CHECK (SocketIndex <= 3), -- aRuneSystem[4]
    CONSTRAINT CK_CharacterRunes_RuneItemId CHECK (RuneItemId > 0),    -- a stored row always means an occupied socket
    CONSTRAINT FK_CharacterRunes_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId)
);
