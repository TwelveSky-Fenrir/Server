-- game.Characters.Zone241Time -- the "aZone241Time" counter (Server/Header/Protocol/STRUCT.h:751-757's
-- persisted avatar snapshot fields; wire-exposed today via AvatarInfo.Zone241Time/CreateAvatarResponse, but
-- always sent as the bare literal 0 -- see Network/Fenrir.Network.Serialization/Packets/Shared/
-- AvatarInfoTemplates.cs:103 and Application/Fenrir.Application.Game.Domain/World/Zone.EconomyMirrors.cs:778's
-- own "Value03 (aZone241Time) has no Fenrir-side field yet" remark).
--
-- First identified durable consumer: the legacy-behavior-translator's Rebirth-advancement contract, Path B
-- ("Max Rebirth", CZ_TRIBE_WORK_SEND tSort 11) -- Server/ts25zone/S04_MyWork02.cpp:11342-11390 increments
-- aZone241Time by 10 unconditionally the moment that path's own preconditions pass (before its
-- rebirth-count-6 cap check even runs). Whether that increment should instead be gated on an ACTUAL
-- successful rebirth transition, per that contract's own "Edge cases" hardening note, is a domain-layer
-- decision made by whichever service calls usp_Character_AdjustZone241Time -- this migration only adds the
-- column those callers need; see that procedure's own header for the write-path primitive.
--
-- Added as a standalone migration (not folded into Tables/game/Characters.sql) since that script is already
-- applied: Fenrir.Tools.DbMigrator journals every script by content hash and refuses a changed re-apply.
-- Not edited in place once applied, same posture as every other migration in this list -- a later
-- correction is a new migration, never an edit of this one.
ALTER TABLE game.Characters
    ADD Zone241Time INT NOT NULL
        CONSTRAINT DF_Characters_Zone241Time DEFAULT 0
        CONSTRAINT CK_Characters_Zone241Time CHECK (Zone241Time >= 0);
GO
