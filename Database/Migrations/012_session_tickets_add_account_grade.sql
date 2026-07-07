-- Threads the same AccountGrade fact minted by 011_accounts_add_account_grade.sql from Login's now-
-- authenticated session, through the single-use handover ticket, into the Zone-side session -- GameServer
-- never re-queries auth.Accounts itself (ADR-0005: it never re-checks credentials at all), so this is the
-- only hand-off point where the fact can cross the process boundary without a per-action DB read.
--
-- Schema change to an already-applied table/procs -- new migration, not an in-place edit, same reasoning
-- as 010_session_tickets_add_session_token.sql. All 3 procs are SCHEMABINDING, so they must be dropped
-- before the ALTER TABLE and recreated after -- all in this one script, batch-split on the standalone GO
-- lines below (CREATE/ALTER PROCEDURE must be alone in its batch).

DROP PROCEDURE runtime.usp_SessionTicket_Create;
DROP PROCEDURE runtime.usp_SessionTicket_Consume;
DROP PROCEDURE runtime.usp_SessionTicket_Purge;
GO

-- Safe: rows here carry only a 15s TTL and no durable meaning, so clearing before the ALTER loses nothing
-- (see 010's own remarks).
DELETE
FROM runtime.SessionTickets;

ALTER TABLE runtime.SessionTickets
    ADD AccountGrade SMALLINT NOT NULL DEFAULT 0;
GO

-- Idempotent: only one active ticket exists per AccountId at any time -- a second login for the same
-- account before the previous ticket is consumed simply supersedes it (DELETE-then-INSERT).
CREATE PROCEDURE runtime.usp_SessionTicket_Create @AccountId INT,
                                                  @CharacterId INT,
                                                  @ShardId TINYINT,
                                                  @TtlSeconds INT,
                                                  @SessionToken UNIQUEIDENTIFIER,
                                                  @AccountGrade SMALLINT
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DELETE
    FROM runtime.SessionTickets
    WHERE AccountId = @AccountId;

    INSERT INTO runtime.SessionTickets (AccountId, CharacterId, ShardId, ExpiresAtUtc, SessionToken, AccountGrade)
    VALUES (@AccountId, @CharacterId, @ShardId, DATEADD(SECOND, @TtlSeconds, SYSUTCDATETIME()), @SessionToken,
            @AccountGrade);
END;
GO

-- Not idempotent, never retryable: read-then-delete-then-select-if-valid. The DELETE always runs, so
-- a replay/duplicate call for the same AccountId finds nothing the second time (single-use ticket;
-- a blind retry here is the classic MMO ticket-dupe bug). AccountGrade is appended as the SELECT's last
-- column (ordinal-mapped) -- ConsumedTicketDto's ctor appends it last too.
CREATE PROCEDURE runtime.usp_SessionTicket_Consume @AccountId INT
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DECLARE
        @CharacterId INT, @ShardId TINYINT, @Exp DATETIME2(3), @SessionToken UNIQUEIDENTIFIER, @AccountGrade SMALLINT;

    SELECT @CharacterId = CharacterId,
           @ShardId = ShardId,
           @Exp = ExpiresAtUtc,
           @SessionToken = SessionToken,
           @AccountGrade = AccountGrade
    FROM runtime.SessionTickets
    WHERE AccountId = @AccountId;

    DELETE
    FROM runtime.SessionTickets
    WHERE AccountId = @AccountId;

    IF
        @Exp IS NOT NULL AND @Exp > SYSUTCDATETIME()
        SELECT @CharacterId  AS CharacterId,
               @ShardId      AS ShardId,
               @SessionToken AS SessionToken,
               @AccountGrade AS AccountGrade;
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
