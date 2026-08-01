-- Lot 1 -- rescope the session ticket to the ZONE.
--
-- Adds runtime.SessionTickets.TargetMapId SMALLINT NOT NULL: the target zone map of a Login->Game
-- (or Game shard A -> Game shard B) hand-off, carried across the process boundary alongside the existing
-- ShardId (which routes to the destination *endpoint/process*). ShardId answers "which server socket does
-- the client reconnect to"; TargetMapId answers "which zone map is it entering there".
--
-- WHY A NEW MIGRATION SCRIPT INSTEAD OF EDITING THE BASE FILES
-- Tables/runtime/SessionTickets.sql and StoredProcedures/runtime/usp_SessionTicket_{Create,Consume,Purge}.sql
-- are already listed in _manifest.txt and are therefore SHA-256-journaled by Tools/Fenrir.Tools.DbMigrator on
-- every database that has ever applied them (the AppHost dev volume is persistent). The migrator refuses to
-- re-apply a journaled path whose content changed -- so editing those base files in place would hard-fail the
-- next migration run on any provisioned database. This additive change therefore lands as a brand-new script:
--   * a FRESH database (the test fixtures rebuild one and replay the whole manifest) applies the base scripts
--     first -- creating the table + procs WITHOUT TargetMapId -- then this migration converts them to the
--     final shape;
--   * a PERSISTENT database skips the already-journaled base scripts (unchanged hash) and applies only this
--     new migration.
-- Both converge on the identical final schema (table + procs carrying TargetMapId). The base files stay at
-- their original shape by design; the "current" definition of these objects is base + this migration.
--
-- WHY DROP+RECREATE RATHER THAN ALTER TABLE ... ADD
-- runtime.SessionTickets is MEMORY_OPTIMIZED with DURABILITY = SCHEMA_ONLY, and its Create/Consume/Purge procs
-- are WITH NATIVE_COMPILATION, SCHEMABINDING -- a schema-bound dependency on the table and its columns. To make
-- the native Create/Consume procs *reference* the new column (INSERT column list, SELECT projection) they must
-- be recreated regardless of whether the column is added via ALTER or a fresh CREATE (Microsoft Learn,
-- "Altering Memory-Optimized Tables" / "Altering Natively Compiled T-SQL Modules"). Given the procs have to be
-- dropped+recreated anyway, and the table holds only ephemeral, worthless-on-crash rows (15s TTL, SCHEMA_ONLY
-- -- see the table's own base-file header), the simplest robust path is: drop the three schema-bound native
-- procs, drop the table (no durable data to preserve), recreate the table with TargetMapId, recreate the
-- procs. usp_SessionTicket_Purge is recreated byte-for-byte identical -- it is dropped ONLY because it
-- schema-binds the table, not because its behavior changes.
--
-- EXECUTE permissions are unaffected: Schemas/002_roles.sql grants EXECUTE at the SCHEMA level
-- (GRANT EXECUTE ON SCHEMA::runtime TO fenrir_login_role / fenrir_game_role), a covering permission that is
-- inherited transitively by every object created in the schema, now and in the future (Microsoft Learn,
-- "Get started with Database Engine permissions" -- schema permissions are transitive to child objects).
-- Dropping and recreating a procedure in the runtime schema therefore loses no grant; the permission lives on
-- the schema, not the object, so this migration is correct whether it runs before or after 002_roles.sql.

-- 1. Drop the three schema-bound native procedures (SCHEMABINDING blocks dropping the table while any of them
--    exists), then drop the SCHEMA_ONLY table itself. IF EXISTS keeps this resilient regardless of start state.
DROP PROCEDURE IF EXISTS runtime.usp_SessionTicket_Consume;
DROP PROCEDURE IF EXISTS runtime.usp_SessionTicket_Create;
DROP PROCEDURE IF EXISTS runtime.usp_SessionTicket_Purge;
DROP TABLE IF EXISTS runtime.SessionTickets;
GO

-- 2. Recreate the table with TargetMapId. Body is identical to Tables/runtime/SessionTickets.sql's final shape
--    plus the new column; keeping PK NONCLUSTERED HASH (AccountId) and IX_SessionTickets_ExpiresAtUtc as-is.
-- Keyed on AccountId, not a random TicketId+Secret: the unmodified legacy client cannot present a
-- GUID/HMAC proof, only its own account identity -- a valid, unexpired ticket existing for that
-- AccountId is itself the proof, since only Login could have written it.
-- SCHEMA_ONLY: a ticket surviving a crash is worthless anyway (15s TTL).
CREATE TABLE runtime.SessionTickets
(
    AccountId    INT              NOT NULL,
    CharacterId  INT              NOT NULL,
    ShardId      TINYINT          NOT NULL, -- routes the client reconnect to the destination server endpoint/process
    TargetMapId  SMALLINT         NOT NULL, -- target zone map the hand-off enters on that endpoint (Lot 1: ticket rescoped to the zone)
    ExpiresAtUtc DATETIME2(3)     NOT NULL,
    SessionToken UNIQUEIDENTIFIER NOT NULL, -- minted by usp_AccountSession_ClaimOrSignalKick at Login-claim time; proves a Game-side world-entry claim is for the same login epoch as the one that minted this ticket
    AccountGrade SMALLINT         NOT NULL, -- carries auth.Accounts.AccountGrade across the Login->Game process boundary (GameServer never re-queries auth.Accounts)
    CONSTRAINT PK_SessionTickets PRIMARY KEY NONCLUSTERED HASH (AccountId)
        WITH (BUCKET_COUNT = 1024),
    -- Supports usp_SessionTicket_Purge's own WHERE ExpiresAtUtc <= SYSUTCDATETIME() sweep. Row count here
    -- stays trivially small (bounded by concurrently in-flight Login->Game hand-offs, TTL 15s) so this is a
    -- minor/free consistency improvement rather than a load-bearing fix the way the same shape is for the
    -- higher-volume runtime.*Relay tables -- see GuildTribeBroadcastRelay's own index comment.
    INDEX IX_SessionTickets_ExpiresAtUtc NONCLUSTERED (ExpiresAtUtc)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY);
GO

-- 3a. Create: @TargetMapId appended as the LAST parameter, included in the INSERT column list + VALUES.
-- Idempotent: only one active ticket exists per AccountId at any time -- a second login for the same
-- account before the previous ticket is consumed simply supersedes it (DELETE-then-INSERT).
-- SessionToken proves a Game-side world-entry claim is for the same login epoch as the one that minted this
-- ticket (see runtime.SessionTickets' own doc comment); AccountGrade carries the Login-side authenticated
-- fact through to the Zone-side session without a re-query of auth.Accounts. TargetMapId carries the
-- destination zone map (Lot 1).
CREATE PROCEDURE runtime.usp_SessionTicket_Create @AccountId INT,
                                                  @CharacterId INT,
                                                  @ShardId TINYINT,
                                                  @TtlSeconds INT,
                                                  @SessionToken UNIQUEIDENTIFIER,
                                                  @AccountGrade SMALLINT,
                                                  @TargetMapId SMALLINT
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DELETE
    FROM runtime.SessionTickets
    WHERE AccountId = @AccountId;

    INSERT INTO runtime.SessionTickets (AccountId, CharacterId, ShardId, TargetMapId, ExpiresAtUtc, SessionToken,
                                        AccountGrade)
    VALUES (@AccountId, @CharacterId, @ShardId, @TargetMapId, DATEADD(SECOND, @TtlSeconds, SYSUTCDATETIME()),
            @SessionToken, @AccountGrade);
END;
GO

-- 3b. Consume: TargetMapId appended as the LAST column of the output SELECT (ordinal-mapped to
--     ConsumedTicketDto's last positional parameter). Read-then-delete-then-select-if-valid semantics
--     preserved exactly.
-- Not idempotent, never retryable: read-then-delete-then-select-if-valid. The DELETE always runs, so
-- a replay/duplicate call for the same AccountId finds nothing the second time (single-use ticket;
-- a blind retry here is the classic MMO ticket-dupe bug). SessionToken/AccountGrade/TargetMapId are appended
-- as the SELECT's last columns (ordinal-mapped) -- ConsumedTicketDto's ctor appends them last too.
CREATE PROCEDURE runtime.usp_SessionTicket_Consume @AccountId INT
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DECLARE
        @CharacterId INT, @ShardId TINYINT, @Exp DATETIME2(3), @SessionToken UNIQUEIDENTIFIER,
        @AccountGrade SMALLINT, @TargetMapId SMALLINT;

    SELECT @CharacterId = CharacterId,
           @ShardId = ShardId,
           @Exp = ExpiresAtUtc,
           @SessionToken = SessionToken,
           @AccountGrade = AccountGrade,
           @TargetMapId = TargetMapId
    FROM runtime.SessionTickets
    WHERE AccountId = @AccountId;

    DELETE
    FROM runtime.SessionTickets
    WHERE AccountId = @AccountId;

    IF @Exp IS NOT NULL AND @Exp > SYSUTCDATETIME()
        SELECT @CharacterId  AS CharacterId,
               @ShardId      AS ShardId,
               @SessionToken AS SessionToken,
               @AccountGrade AS AccountGrade,
               @TargetMapId  AS TargetMapId;
END;
GO

-- 3c. Purge: recreated byte-for-byte identical to the base file -- dropped ONLY because it schema-binds the
--     table, its behavior is unchanged.
-- Called on a server-side timer, not from the client path: runtime.SessionTickets is SCHEMA_ONLY
-- memory-optimized and has no background cleanup of its own -- expired rows sit there until deleted.
CREATE PROCEDURE runtime.usp_SessionTicket_Purge
    WITH NATIVE_COMPILATION ,
        SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DELETE
    FROM runtime.SessionTickets
    WHERE ExpiresAtUtc <= SYSUTCDATETIME();
END;
GO
