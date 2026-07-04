-- Additive V9 Progression migration for game.Characters (Server Logic chapter, quests/missions/auto-hunt/pet).
-- Same rationale as Characters_progression.sql/Characters_social.sql: the migrator journal
-- (admin.SchemaVersions) forbids changing an applied script's content -- a NEW additive script is the
-- sanctioned corrective path (_manifest.txt header).
-- Legacy mapping (wAvatar/AVATAR_INFO, STRUCT.h):
--   JoinWar/MissionKillOtherTribe/MissionKillMonster/MissionPlayTime <- wAvatar.aMissionDate.{aJoinWar,
--       aKillOtherTribe,aKillMonster,aPlayTime} (MISSION_DATE, STRUCT.h:298-304) -- a SEPARATE struct from
--       the top-level aKillOtherTribe (CP, already ContributionPoints above); MissionKillOtherTribe is
--       named distinctly here to avoid any confusion with that unrelated column. Verified
--       S04_MyWork02.cpp:14521 (W_MISSION_COMPLETE_SEND): aJoinWar and aMissionDate.aKillOtherTribe gate
--       claiming the daily reward (>=1 / >=10, both debited by the claimed amount on success);
--       aKillMonster/aPlayTime are read/written too but their OWN gate is compiled out in EU33
--       (USE_DAILY_UI_MONSTER_KILL is OFF) -- tracked anyway since ZC_MISSION_COMPLETE_RECV echoes all 4.
--   AutoHuntEnabled  <- wAvatar.aAutoState (0/1, W_AUTO_CONFIG_SEND tSort, S04_MyWork02.cpp:13466).
--   AutoHuntConfig   <- wAvatar.aAutoHunt (AUTO_HUNT, 112 o, CopyMemory'd verbatim from the client with NO
--       server-side content validation per the verified source -- stored as an opaque blob for exactly
--       that reason, never decomposed into columns). NOT NULL (empty binary default, not SQL NULL):
--       CaeriusNet's [GenerateDto] source generator emits a plain GetFieldValue<byte[]> call with no
--       IsDBNull guard for this column (matching its own byte[]-must-be-bare-syntax convention, see
--       CharacterWorldSnapshotDto's own remarks) -- a nullable column would throw SqlNullValueException on
--       every fresh character. An empty (zero-length) byte[] round-trips cleanly through
--       AutoHunt.TryRead (fails length validation, correctly falls back to "not configured yet" at the
--       application layer) without ever being SQL NULL.
--   AutoLifeRatio/AutoManaRatio <- wAvatar.aAutoLifeRatio/aAutoManaRatio (CZ_CHANGE_AUTO_INFO, 0..5).
--   PetGrowth/PetActivity <- a Fenrir-side SIMPLIFICATION of wAvatar.aInventory[EPET-slot][4]
--       (pGrowUpValue, up to 640,000,000 -- report 12 §2.1) and wAvatar.aEquip[EPET][1] (activity, 0-100).
--       The legacy persists growth PER PET ITEM INSTANCE (so swapping pets preserves each one's own
--       growth); this migration deliberately tracks ONE active counter per CHARACTER instead of adding a
--       wide-int column to the already-gated, heavily-tested game.CharacterItems table for a single
--       item-sort special case -- reset to that pet's base tier whenever a NEWLY EQUIPPED pet item differs
--       from the previously equipped one (Fenrir.Application.Game.Pets, Zone's own equip-change hook).
--       Documented simplification (StructuredOutput open issue), not a guess.
ALTER TABLE game.Characters
    ADD JoinWar               INT     NOT NULL CONSTRAINT DF_Characters_JoinWar DEFAULT 0,
        MissionKillOtherTribe INT     NOT NULL CONSTRAINT DF_Characters_MissionKillOtherTribe DEFAULT 0,
        MissionKillMonster    INT     NOT NULL CONSTRAINT DF_Characters_MissionKillMonster DEFAULT 0,
        MissionPlayTime       INT     NOT NULL CONSTRAINT DF_Characters_MissionPlayTime DEFAULT 0,
        AutoHuntEnabled       BIT     NOT NULL CONSTRAINT DF_Characters_AutoHuntEnabled DEFAULT 0,
        AutoHuntConfig        VARBINARY(112) NOT NULL CONSTRAINT DF_Characters_AutoHuntConfig DEFAULT 0x,
        AutoLifeRatio         TINYINT NOT NULL CONSTRAINT DF_Characters_AutoLifeRatio DEFAULT 0,
        AutoManaRatio         TINYINT NOT NULL CONSTRAINT DF_Characters_AutoManaRatio DEFAULT 0,
        PetGrowth             INT     NOT NULL CONSTRAINT DF_Characters_PetGrowth DEFAULT 0,
        PetActivity           TINYINT NOT NULL CONSTRAINT DF_Characters_PetActivity DEFAULT 0;
GO

ALTER TABLE game.Characters
    ADD CONSTRAINT CK_Characters_JoinWar CHECK (JoinWar >= 0),
        CONSTRAINT CK_Characters_MissionKillOtherTribe CHECK (MissionKillOtherTribe >= 0),
        CONSTRAINT CK_Characters_MissionKillMonster CHECK (MissionKillMonster >= 0),
        CONSTRAINT CK_Characters_MissionPlayTime CHECK (MissionPlayTime >= 0),
        CONSTRAINT CK_Characters_AutoLifeRatio CHECK (AutoLifeRatio BETWEEN 0 AND 5),
        CONSTRAINT CK_Characters_AutoManaRatio CHECK (AutoManaRatio BETWEEN 0 AND 5),
        CONSTRAINT CK_Characters_PetGrowth CHECK (PetGrowth >= 0),
        CONSTRAINT CK_Characters_PetActivity CHECK (PetActivity BETWEEN 0 AND 100);
GO
