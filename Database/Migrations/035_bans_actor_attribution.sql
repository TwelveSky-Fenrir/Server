-- gm-action-audit-attribution (Critical, part of the EventLogCategory.GmAction wiring round): admin.Bans has
-- never carried any column identifying which GM performed a GM-issued ban -- Tables/admin/Bans.sql (already
-- applied/journaled, never edited in place) only has BanId/AccountId/CharacterId/Reason/ExpiresAtUtc/
-- CreatedAtUtc, all describing the ban's TARGET, none describing its ACTOR. GmBlockAvatarService (the one GM
-- feature Fenrir implements today, legacy case 519 "[GM]-BLOCK") writes a ban row via admin.usp_Ban_Create with
-- no attribution of the acting GM at all. See the legacy-behavior-translator contract "Durable GM-action audit
-- logging (EventLogCategory.GmAction) across the legacy GM-command telemetry family" for the full citation
-- trail (Server/ts25zone/H06_MyUpperCom.h:267-269,298-303,321-330; ServerDocs/12_ts25zone/
-- 18_UpperCom_Center_GameLog.md §3.1/§3.6).
--
-- That contract explicitly separates two decisions: (1) wiring EventLogCategory.GmAction itself (an
-- Application-layer LogAsync call-site change, out of this database-engineer's Infrastructure/Database charter)
-- and (2) whether admin.Bans itself also needs an actor column, or whether cross-referencing game.EventLog by
-- timestamp/target is an accepted substitute. This migration resolves (2): admin.Bans gains its own
-- ActorAccountId/ActorCharacterId, independently nullable exactly like the existing target AccountId/
-- CharacterId pair (a system-imposed or legacy-import ban legitimately has no GM actor), so the ban row itself
-- is self-attributing rather than depending solely on a best-effort EventLog cross-reference.
--
-- Deliberately NO foreign key on either new column -- the same rationale Tables/game/EventLog.sql's own header
-- comment already gives for its own Actor*/Target* columns: a permanent audit-attribution field must survive
-- the deletion of the account/character it references, and must never block that deletion. Unlike the existing
-- target AccountId/CharacterId columns (which DO carry an FK, and which
-- Migrations/017_character_delete_moderation_tribebank_cleanup.sql already has to null out/delete around on
-- character deletion), an FK'd actor column would require that same cleanup-on-delete treatment the moment a
-- GM's own character were ever deleted -- an unnecessary coupling for a field whose whole purpose is durable
-- attribution, not referential integrity. No change to usp_Character_Delete/CleanupChildTables is needed here
-- as a result.
ALTER TABLE admin.Bans
    ADD ActorAccountId INT NULL,
        ActorCharacterId INT NULL;
GO

-- Investigative access pattern ("every ban this GM issued") -- same filtered-nonclustered shape as
-- game.EventLog's own IX_EventLog_ActorCharacterId, filtered because the large majority of historical/
-- system-imposed rows will never populate these columns.
CREATE INDEX IX_Bans_ActorAccount ON admin.Bans (ActorAccountId) INCLUDE (CreatedAtUtc, Reason)
    WHERE ActorAccountId IS NOT NULL;
GO

CREATE INDEX IX_Bans_ActorCharacter ON admin.Bans (ActorCharacterId) INCLUDE (CreatedAtUtc, Reason)
    WHERE ActorCharacterId IS NOT NULL;
GO

-- Corrective follow-up to StoredProcedures/admin/usp_Ban_Create.sql (never edited in place -- DbMigrator
-- journals every script by content hash and refuses a changed re-apply). @ActorAccountId/@ActorCharacterId are
-- appended as new trailing optional parameters (default NULL), never inserted mid-signature, so every existing
-- caller -- including a raw ad hoc EXEC -- keeps binding correctly. Both are independently nullable: a GM
-- performs a ban action from a single live session that carries both its own account id and character id
-- (mirroring the existing @AccountId/@CharacterId target parameters' own independent-nullability shape), but
-- neither being non-null is still a valid call (e.g. a ban created by a future automated/system path with no
-- GM actor), so no new THROW/validation is added for these two.
CREATE OR ALTER PROCEDURE admin.usp_Ban_Create @AccountId INT NULL,
                                               @CharacterId INT NULL,
                                               @Reason TINYINT,
                                               @ExpiresAtUtc DATETIME2(3) = NULL,
                                               @ActorAccountId INT = NULL,
                                               @ActorCharacterId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @AccountId IS NULL AND @CharacterId IS NULL
        THROW 50301, N'A ban must target at least one of @AccountId or @CharacterId.', 1;

    INSERT INTO admin.Bans (AccountId, CharacterId, Reason, ExpiresAtUtc, ActorAccountId, ActorCharacterId)
    OUTPUT INSERTED.BanId
    VALUES (@AccountId, @CharacterId, @Reason, @ExpiresAtUtc, @ActorAccountId, @ActorCharacterId);
END;
GO
