-- No-op if the row is no longer owned by this exact (ServerKind, ShardId, SessionToken) triple -- mirrors
-- usp_AccountSession_ClearIfOwner's own ownership match exactly, the very next step in the same teardown
-- sequence. Without this match, an ordinary Login-to-Game handoff race could either wrongly reject a
-- legitimate player as SessionSuperseded (Login-teardown-first) or permanently poison a live Game-owned row
-- into ConflictTearingDown (Game-claim-first, since nothing else ever transitions SessionState back to 0
-- short of the row being deleted outright). Called best-effort from both connection hosts' finally blocks,
-- wrapped in try/catch-log there so a failure here must never throw out of a finally block.
CREATE PROCEDURE runtime.usp_AccountSession_MarkTearingDown @AccountId INT,
                                                            @ServerKind TINYINT,
                                                            @ShardId TINYINT NULL,
                                                            @SessionToken UNIQUEIDENTIFIER
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    UPDATE runtime.AccountSessions
    SET SessionState = 1
    WHERE AccountId = @AccountId
      AND ServerKind = @ServerKind
      AND (@ShardId IS NULL OR ShardId = @ShardId)
      AND SessionToken = @SessionToken;
END;
