CREATE PROCEDURE runtime.usp_AccountSession_TransitionToGame @AccountId INT,
                                                             @ExpectedSessionToken UNIQUEIDENTIFIER,
                                                             @ShardId TINYINT
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    UPDATE runtime.AccountSessions
    SET ServerKind       = 1,
        ShardId          = @ShardId,
        LastRefreshedUtc = SYSUTCDATETIME()
    WHERE AccountId = @AccountId
      AND SessionToken = @ExpectedSessionToken
      AND ServerKind IN (0, 1)
      AND SessionState = 0
      AND KickRequested = 0;

    SELECT CAST(CASE WHEN @@ROWCOUNT = 1 THEN 1 ELSE 0 END AS BIT) AS Accepted;
END;
