-- Durable per-character "logout info" snapshot -- Fenrir's stand-in for the legacy avatar record's
-- UPDATE_LOGOUT_INFO[6] array (Server/Header/Protocol/DEFINE.h:368 -- the array holds SIX elements;
-- DEFINE.h:750-757 -- element 0 = zone/server number, 1-3 = position, 4 = life, 5 = mana), which in legacy
-- was serialized straight onto the avatar and persisted to six dedicated columns (Server/Header/
-- CSQLAvatar.cpp:707-712, Server/BuildEU33/DB/nxtserver.sql:73-78, "default 0"). Fenrir had no equivalent at
-- all: the six values were never populated, so a reconnect could never carry the client's last-session
-- zone/position/life/mana back to it (Party&session/reward cross-shard state finding, workstream D1).
--
-- DELIBERATELY a SEPARATE disk table keyed by CharacterId (one-to-one, LEFT-JOIN-friendly, "no row = never
-- captured, treated as all-zero" -- the same shape game.CharacterQuests uses), NOT six more columns bolted
-- onto game.Characters: game.Characters is already the authoritative live-placement store (PosX/PosY/PosZ via
-- usp_Character_PersistBatch, Life/Mana via usp_Character_PersistProgressBatch/usp_Character_ClampVitalsFloor).
-- This table is a distinct, capture-time snapshot used only for the client-facing LogoutInfo wire array and
-- for last-zone redirect selection -- it must never become a second authority for the live world spawn, so it
-- is kept out of the world-entry bundle read on purpose.
--
-- LastZone is the map/zone number the character was last registered into (used by the login redirect to pick
-- a destination shard, and by the legacy tribe-vs-last-zone consistency correction -- see
-- Application/Fenrir.Application.Login.Domain/LoginTrain.cs's own remarks for why that correction stays
-- deferred: the four fixed tribe-specific town-spawn value sets it needs were never enumerated by the
-- behavior contract). Position is stored as three whole numbers (the legacy capture truncates the
-- floating-point action location, DEFINE.h:750-757); Life/Mana are whole numbers. No upper range is enforced
-- at capture, matching legacy (the low-word floor legacy applies at restore is a read-side concern, not a
-- storage constraint -- and its exact bit semantics were flagged unverified by the contract).
CREATE TABLE game.CharacterLogoutState
(
    CharacterId   INT          NOT NULL,
    LastZone      INT          NOT NULL
        CONSTRAINT DF_CharacterLogoutState_LastZone DEFAULT 0,
    PosX          INT          NOT NULL
        CONSTRAINT DF_CharacterLogoutState_PosX DEFAULT 0,
    PosY          INT          NOT NULL
        CONSTRAINT DF_CharacterLogoutState_PosY DEFAULT 0,
    PosZ          INT          NOT NULL
        CONSTRAINT DF_CharacterLogoutState_PosZ DEFAULT 0,
    Life          INT          NOT NULL
        CONSTRAINT DF_CharacterLogoutState_Life DEFAULT 0,
    Mana          INT          NOT NULL
        CONSTRAINT DF_CharacterLogoutState_Mana DEFAULT 0,
    -- Self-referential write-behind guard for usp_CharacterLogoutState_PersistBatch (compares against this
    -- table's own prior value) -- deliberately NOT borrowed from game.Characters.FlushSequence, which
    -- advances independently on usp_Character_PersistBatch/usp_Character_PersistProgressBatch's own cadence;
    -- comparing against that column here would make a legitimate LogoutState capture silently lose the
    -- idempotence race against whichever of those two batches for the same tick happens to land first.
    FlushSequence BIGINT       NOT NULL
        CONSTRAINT DF_CharacterLogoutState_FlushSequence DEFAULT 0,
    CapturedAtUtc DATETIME2(3) NOT NULL,
    CONSTRAINT PK_CharacterLogoutState PRIMARY KEY CLUSTERED (CharacterId),
    -- ON DELETE CASCADE, deliberately UNLIKE the other game.Character* child tables (CharacterQuests et al.,
    -- which use a no-cascade FK cleaned up explicitly inside usp_Character_Delete): that procedure is already
    -- shipped and must never be edited (the migrator refuses a changed-hash re-apply), and it does NOT delete
    -- from this table -- so without a cascade its final DELETE FROM game.Characters would hit an FK violation
    -- for any character that ever entered the world. This table is a pure dependent snapshot with no
    -- audit-preservation requirement (contrast game.TribeBankLog), so cascading its removal with the character
    -- is exactly right and keeps character deletion working with zero change to the shipped delete proc.
    CONSTRAINT FK_CharacterLogoutState_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId)
        ON DELETE CASCADE
);
