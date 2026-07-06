-- Called at world-entry. Defers entirely to the same authority as usp_AccountSession_ClaimOrSignalKick --
-- this is a narrower read/write against the same row, not a second decision tree. Accepted only when the
-- account still holds the Login-side session matching @ExpectedSessionToken, which proves this claim is for
-- the same login epoch as the one that minted the SessionTicket carrying that token, not a hijack of a
-- newer login that raced ahead of the hand-off.
CREATE PROCEDURE runtime.usp_AccountSession_TransitionToGame @AccountId            INT,
    @ExpectedSessionToken UNIQUEIDENTIFIER,
    @ShardId              TINYINT
WITH NATIVE_COMPILATION, SCHEMABINDING
AS
BEGIN ATOMIC
WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
UPDATE runtime.AccountSessions
SET ServerKind       = 1,
    ShardId          = @ShardId,
    LastRefreshedUtc = SYSUTCDATETIME()
WHERE AccountId = @AccountId
  AND SessionToken = @ExpectedSessionToken
  AND ServerKind = 0
  AND SessionState = 0
  AND KickRequested = 0;

SELECT CAST(CASE WHEN @@ROWCOUNT = 1 THEN 1 ELSE 0 END AS BIT) AS Accepted;
END;
