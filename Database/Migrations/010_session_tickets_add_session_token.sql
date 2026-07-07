-- Session-token hand-off: runtime.SessionTickets now carries the SessionToken minted by
-- usp_AccountSession_ClaimOrSignalKick at Login-claim time, so usp_AccountSession_TransitionToGame can prove
-- a Game-side world-entry claim is for the same login epoch as the one that minted this ticket, not a
-- hijack of a newer login that raced ahead of the hand-off.
--
-- This is a schema change to an already-applied table/procs, so per the "never edit an already-applied
-- script" rule it is a new migration rather than an edit of Tables/runtime/SessionTickets.sql or
-- StoredProcedures/runtime/usp_SessionTicket_Create/_Consume/_Purge.sql in place. The 3 procs are
-- SCHEMABINDING, so they must be dropped before the ALTER TABLE and recreated after -- all in this one
-- script, batch-split on the standalone GO lines below (CREATE/ALTER PROCEDURE must be alone in its batch).

DROP PROCEDURE runtime.usp_SessionTicket_Create;
DROP PROCEDURE runtime.usp_SessionTicket_Consume;
DROP PROCEDURE runtime.usp_SessionTicket_Purge;
GO

-- Safe: rows here carry only a 15s TTL and no durable meaning, so clearing before the ALTER loses nothing.
DELETE
FROM runtime.SessionTickets;

-- A DEFAULT is required by ALTER TABLE ... ADD for a NOT NULL column regardless of the table currently
-- having zero rows (see ALTER TABLE column_constraint's NULL | NOT NULL remarks) -- this default is never
-- actually read since the table is empty and every future INSERT (the recreated usp_SessionTicket_Create,
-- below) always supplies a real SessionToken explicitly.
ALTER TABLE runtime.SessionTickets
    ADD SessionToken UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
GO

-- Idempotent: only one active ticket exists per AccountId at any time -- a second login for the same
-- account before the previous ticket is consumed simply supersedes it (DELETE-then-INSERT).
CREATE PROCEDURE runtime.usp_SessionTicket_Create @AccountId INT,
                                                  @CharacterId INT,
                                                  @ShardId TINYINT,
                                                  @TtlSeconds INT,
                                                  @SessionToken UNIQUEIDENTIFIER
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DELETE
    FROM runtime.SessionTickets
    WHERE AccountId = @AccountId;

    INSERT INTO runtime.SessionTickets (AccountId, CharacterId, ShardId, ExpiresAtUtc, SessionToken)
    VALUES (@AccountId, @CharacterId, @ShardId, DATEADD(SECOND, @TtlSeconds, SYSUTCDATETIME()), @SessionToken);
END;
GO

-- Not idempotent, never retryable: read-then-delete-then-select-if-valid. The DELETE always runs, so
-- a replay/duplicate call for the same AccountId finds nothing the second time (single-use ticket;
-- a blind retry here is the classic MMO ticket-dupe bug). SessionToken is appended as the SELECT's last
-- column (ordinal-mapped) -- ConsumedTicketDto's ctor appends it last too.
CREATE PROCEDURE runtime.usp_SessionTicket_Consume @AccountId INT
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DECLARE
        @CharacterId INT, @ShardId TINYINT, @Exp DATETIME2(3), @SessionToken UNIQUEIDENTIFIER;

    SELECT @CharacterId = CharacterId, @ShardId = ShardId, @Exp = ExpiresAtUtc, @SessionToken = SessionToken
    FROM runtime.SessionTickets
    WHERE AccountId = @AccountId;

    DELETE
    FROM runtime.SessionTickets
    WHERE AccountId = @AccountId;

    IF
        @Exp IS NOT NULL AND @Exp > SYSUTCDATETIME()
        SELECT @CharacterId AS CharacterId, @ShardId AS ShardId, @SessionToken AS SessionToken;
END;
GO

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
