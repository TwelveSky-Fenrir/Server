-- Idempotent: only one active ticket exists per AccountId at any time -- a second login for the same
-- account before the previous ticket is consumed simply supersedes it (DELETE-then-INSERT).
-- SessionToken proves a Game-side world-entry claim is for the same login epoch as the one that minted this
-- ticket (see runtime.SessionTickets' own doc comment); AccountGrade carries the Login-side authenticated
-- fact through to the Zone-side session without a re-query of auth.Accounts.
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
